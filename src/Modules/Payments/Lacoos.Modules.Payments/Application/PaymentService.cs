using Lacoos.Modules.Payments.Domain;
using Lacoos.Modules.Payments.Persistence;
using Lacoos.SharedKernel.Files;
using Lacoos.SharedKernel.Persistence;
using Microsoft.AspNetCore.Http;

namespace Lacoos.Modules.Payments.Application;

internal sealed record PaymentFilter(string? Search, Guid? SupplierId, string? Status, string? View);

internal sealed record SavePaymentRequest(
    string? Description,
    decimal? TotalAmount,
    PaymentMethod? PaymentMethod,
    Guid? SupplierId,
    Guid? BudgetItemId,
    string? Notes,
    int? InstallmentCount,
    DateOnly? FirstDueDate,
    IReadOnlyList<DateOnly>? DueDates);

internal sealed record UpdateInstallmentRequest(DateOnly? DueDate);

internal sealed record SettleRequest(decimal? Amount, PaymentMethod? PaymentMethod, DateOnly? PaidAt, string? Notes);

internal sealed record ReversalRequest(bool? Confirm, string? Reason);

internal sealed record ReceiptResponse(
    Guid Id, Guid InstallmentId, Guid? TransactionId, string FileName, string ContentType, long Size, DateTimeOffset UploadedAt);

internal sealed record InstallmentResponse(
    Guid Id,
    int InstallmentNumber,
    decimal Amount,
    DateOnly DueDate,
    DateOnly? PaidAt,
    decimal PaidAmount,
    decimal RemainingAmount,
    PaymentMethod? PaymentMethod,
    string? Notes,
    IReadOnlyList<ReceiptResponse> Receipts,
    PaymentStatus Status);

internal sealed record PaymentResponse(
    Guid Id,
    string Description,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    Guid? SupplierId,
    string? SupplierName,
    Guid? BudgetItemId,
    string? BudgetItemName,
    string? Notes,
    decimal PaidAmount,
    decimal RemainingAmount,
    PaymentStatus Status,
    IReadOnlyList<InstallmentResponse> Installments);

internal sealed record PaymentTotals(
    decimal Total, decimal Paid, decimal Open, decimal OverdueAmount, int OverdueCount, decimal PaidPercent);

internal sealed record PaymentListResponse(IReadOnlyList<PaymentResponse> Items, PaymentTotals Totals);

internal sealed record TransactionResponse(
    Guid Id,
    TransactionType Type,
    decimal Amount,
    DateOnly PaidAt,
    PaymentMethod? PaymentMethod,
    string? Notes,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt);

internal sealed class PaymentService(
    PaymentsDbContext db,
    ISupplierQueries suppliers,
    IBudgetQueries budget,
    IFileStorage files,
    IClock clock,
    ICurrentUser currentUser,
    IWeddingContext wedding)
{
    public async Task<PaymentListResponse> ListAsync(PaymentFilter filter, CancellationToken ct)
    {
        var status = EnumText.ParseFilter<PaymentStatus>(filter.Status, "status");
        var today = clock.Today;
        var all = await LoadAll().ToListAsync(ct);
        IEnumerable<Payment> matches = all;

        if (Text.Clean(filter.Search) is { } search)
        {
            var supplierIds = (await suppliers.SearchIdsAsync(search, ct)).ToHashSet();
            var itemIds = (await budget.SearchItemIdsAsync(search, ct)).ToHashSet();
            matches = matches.Where(p =>
                p.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (p.SupplierId is { } supplierId && supplierIds.Contains(supplierId))
                || (p.BudgetItemId is { } itemId && itemIds.Contains(itemId)));
        }

        if (filter.SupplierId is not null)
        {
            matches = matches.Where(p => p.SupplierId == filter.SupplierId);
        }

        if (status is not null)
        {
            matches = matches.Where(p => p.StatusOn(today) == status);
        }

        matches = Text.Clean(filter.View) switch
        {
            null => matches,
            "pending" => matches.Where(p => p.StatusOn(today) is PaymentStatus.Pending or PaymentStatus.Partial),
            "overdue" => matches.Where(p => p.StatusOn(today) == PaymentStatus.Overdue),
            "paid" => matches.Where(p => p.StatusOn(today) == PaymentStatus.Paid),
            _ => throw new ValidationException(new Dictionary<string, string[]> { ["view"] = ["Valor inválido para view."] }),
        };

        var items = matches.OrderBy(p => p.Installments.Min(i => i.DueDate)).ThenBy(p => p.Description).ToList();
        return new PaymentListResponse(await ToResponsesAsync(items, ct), ToTotals(all, today));
    }

    public async Task<PaymentResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var payment = await LoadAll().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Pagamento não encontrado.");
        return (await ToResponsesAsync([payment], ct))[0];
    }

    public async Task<PaymentResponse> CreateAsync(SavePaymentRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, ct);

        var count = request.InstallmentCount ?? request.DueDates?.Count ?? PaymentRules.MinInstallments;
        var validator = new Validator().Range(count, PaymentRules.MinInstallments, PaymentRules.MaxInstallments, "installmentCount", "Número de parcelas");
        if (request.DueDates is null && request.FirstDueDate is null)
        {
            validator.Add("firstDueDate", "Primeiro vencimento é obrigatório.");
        }

        if (request.DueDates is not null && request.DueDates.Count != count)
        {
            validator.Add("dueDates", "Informe um vencimento para cada parcela.");
        }

        validator.ThrowIfInvalid();

        var payment = new Payment { Description = request.Description!.Trim() };
        Apply(payment, request);
        payment.TotalAmount = request.TotalAmount!.Value;

        var dueDates = request.DueDates ?? PaymentRules.MonthlyDueDates(request.FirstDueDate!.Value, count);
        payment.Installments = BuildInstallments(payment.TotalAmount, dueDates);

        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        return await GetAsync(payment.Id, ct);
    }

    public async Task<PaymentResponse> UpdateAsync(Guid id, SavePaymentRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, ct);
        var count = request.InstallmentCount;
        if (count is not null)
        {
            new Validator().Range(count.Value, PaymentRules.MinInstallments, PaymentRules.MaxInstallments, "installmentCount", "Número de parcelas").ThrowIfInvalid();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var payment = await db.Payments
            .Include(p => p.Installments).ThenInclude(i => i.Transactions)
            .Include(p => p.Installments).ThenInclude(i => i.Receipts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Pagamento não encontrado.");

        foreach (var installment in payment.Installments)
        {
            await db.LockRowAsync(PaymentsDbContext.InstallmentsTable, installment.Id, ct);
        }

        var newTotal = request.TotalAmount!.Value;
        var newCount = count ?? payment.Installments.Count;
        var totalChanged = newTotal != payment.TotalAmount;
        var countChanged = newCount != payment.Installments.Count;

        if (totalChanged || countChanged)
        {
            // Com baixas registradas, recalcular mudaria parcelas que já têm histórico.
            var installmentIds = payment.Installments.Select(i => i.Id).ToList();
            if (await db.Transactions.AnyAsync(t => installmentIds.Contains(t.InstallmentId), ct))
            {
                throw new BusinessRuleException("Não é possível alterar o valor total ou as parcelas de um pagamento com baixas registradas.");
            }

            if (countChanged)
            {
                await ReplaceInstallmentsAsync(payment, request, newTotal, newCount, ct);
            }
            else
            {
                Redistribute(payment, newTotal);
            }
        }

        payment.Description = request.Description!.Trim();
        Apply(payment, request);
        payment.TotalAmount = newTotal;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var payment = await db.Payments
            .Include(p => p.Installments).ThenInclude(i => i.Transactions)
            .Include(p => p.Installments).ThenInclude(i => i.Receipts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Pagamento não encontrado.");

        // Estornos também contam: o histórico da parcela precisa ser preservado.
        if (payment.Installments.Any(i => i.Transactions.Count > 0))
        {
            throw new ConflictException("Não é possível excluir um pagamento com baixas registradas.");
        }

        var fileKeys = payment.Installments.SelectMany(i => i.Receipts).Select(r => r.FileKey).ToList();
        db.Payments.Remove(payment);
        await db.SaveChangesAsync(ct);

        foreach (var key in fileKeys)
        {
            await files.DeleteAsync(key, ct);
        }
    }

    public async Task<PaymentResponse> UpdateInstallmentAsync(Guid paymentId, int number, UpdateInstallmentRequest request, CancellationToken ct)
    {
        new Validator().Required(request.DueDate, "dueDate", "Vencimento").ThrowIfInvalid();

        var installment = await db.Installments
            .FirstOrDefaultAsync(i => i.PaymentId == paymentId && i.InstallmentNumber == number, ct)
            ?? throw new NotFoundException("Parcela não encontrada.");
        installment.DueDate = request.DueDate!.Value;
        await db.SaveChangesAsync(ct);
        return await GetAsync(paymentId, ct);
    }

    public async Task<IReadOnlyList<TransactionResponse>> ListTransactionsAsync(Guid installmentId, CancellationToken ct)
    {
        await EnsureInstallmentExistsAsync(installmentId, ct);
        var transactions = await db.Transactions.AsNoTracking()
            .Where(t => t.InstallmentId == installmentId)
            .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .ToListAsync(ct);
        return transactions.Select(ToResponse).ToList();
    }

    public async Task<TransactionResponse> SettleAsync(Guid installmentId, SettleRequest request, CancellationToken ct)
    {
        var validator = new Validator()
            .Required(request.Amount, "amount", "Valor")
            .Money(request.Amount, "amount", "Valor");
        if (request.Amount is 0)
        {
            validator.Add("amount", "Valor deve ser maior que zero.");
        }

        validator.ThrowIfInvalid();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var installment = await LoadLockedInstallmentAsync(installmentId, ct);

        if (request.Amount!.Value > installment.RemainingAmount)
        {
            throw new BusinessRuleException($"A baixa excede o valor restante da parcela ({Text.Brl(installment.RemainingAmount)}).");
        }

        var method = request.PaymentMethod
            ?? await db.Payments.Where(p => p.Id == installment.PaymentId).Select(p => p.PaymentMethod).FirstAsync(ct);

        var entry = NewTransaction(installment.Id, TransactionType.Settlement, request.Amount.Value);
        entry.PaidAt = request.PaidAt ?? clock.Today;
        entry.PaymentMethod = method;
        entry.Notes = Text.Clean(request.Notes);
        db.Transactions.Add(entry);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(entry);
    }

    public async Task<TransactionResponse> ReverseAsync(Guid installmentId, ReversalRequest request, CancellationToken ct)
    {
        var validator = new Validator().Required(request.Reason, "reason", "Motivo do estorno");
        if (request.Confirm != true)
        {
            validator.Add("confirm", "Confirme o estorno enviando confirm como true.");
        }

        validator.ThrowIfInvalid();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var installment = await LoadLockedInstallmentAsync(installmentId, ct);

        var paid = installment.PaidAmount;
        if (paid <= 0)
        {
            throw new BusinessRuleException("Esta parcela não tem valor pago para estornar.");
        }

        var entry = NewTransaction(installment.Id, TransactionType.Reversal, paid);
        entry.PaidAt = clock.Today;
        entry.Notes = request.Reason!.Trim();
        db.Transactions.Add(entry);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return ToResponse(entry);
    }

    public async Task<ReceiptResponse> AddReceiptAsync(Guid installmentId, Guid? transactionId, IFormFile? file, CancellationToken ct)
    {
        await EnsureInstallmentExistsAsync(installmentId, ct);

        if (transactionId is { } id
            && !await db.Transactions.AnyAsync(t => t.Id == id && t.InstallmentId == installmentId, ct))
        {
            throw new BusinessRuleException("A baixa informada não pertence a esta parcela.");
        }

        var stored = await files.StoreAsync(file, "receipts", wedding.WeddingId!.Value, ct);
        var receipt = new PaymentReceipt
        {
            InstallmentId = installmentId,
            TransactionId = transactionId,
            FileName = stored.FileName,
            FileKey = stored.Key,
            ContentType = stored.ContentType,
            Size = stored.Size,
            UploadedAt = clock.UtcNow,
        };
        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);
        return ToResponse(receipt);
    }

    public async Task<IResult> DownloadReceiptAsync(Guid id, CancellationToken ct)
    {
        var receipt = await LoadReceiptAsync(id, ct);
        return await files.DownloadAsync(receipt.FileKey, receipt.FileName, receipt.ContentType, ct);
    }

    public async Task DeleteReceiptAsync(Guid id, CancellationToken ct)
    {
        var receipt = await LoadReceiptAsync(id, ct);
        db.Receipts.Remove(receipt);
        await db.SaveChangesAsync(ct);
        await files.DeleteAsync(receipt.FileKey, ct);
    }

    private IQueryable<Payment> LoadAll() =>
        db.Payments.AsNoTracking()
            .Include(p => p.Installments).ThenInclude(i => i.Transactions)
            .Include(p => p.Installments).ThenInclude(i => i.Receipts)
            .AsSplitQuery();

    private async Task<PaymentInstallment> LoadLockedInstallmentAsync(Guid id, CancellationToken ct)
    {
        // A linha fica bloqueada até o commit: duas baixas simultâneas não passam juntas pela validação.
        await db.LockRowAsync(PaymentsDbContext.InstallmentsTable, id, ct);
        return await db.Installments.Include(i => i.Transactions).FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Parcela não encontrada.");
    }

    private async Task EnsureInstallmentExistsAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Installments.AnyAsync(i => i.Id == id, ct))
        {
            throw new NotFoundException("Parcela não encontrada.");
        }
    }

    private async Task<PaymentReceipt> LoadReceiptAsync(Guid id, CancellationToken ct) =>
        await db.Receipts.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("Comprovante não encontrado.");

    private InstallmentTransaction NewTransaction(Guid installmentId, TransactionType type, decimal amount) => new()
    {
        InstallmentId = installmentId,
        Type = type,
        Amount = amount,
        CreatedByUserId = currentUser.RequiredUserId,
        CreatedAt = clock.UtcNow,
    };

    private async Task ReplaceInstallmentsAsync(Payment payment, SavePaymentRequest request, decimal total, int count, CancellationToken ct)
    {
        if (payment.Installments.Any(i => i.Receipts.Count > 0))
        {
            throw new BusinessRuleException("Não é possível alterar o número de parcelas de um pagamento com comprovantes.");
        }

        var first = request.FirstDueDate ?? payment.Installments.Min(i => i.DueDate);
        var dueDates = request.DueDates ?? PaymentRules.MonthlyDueDates(first, count);
        if (dueDates.Count != count)
        {
            new Validator().Add("dueDates", "Informe um vencimento para cada parcela.").ThrowIfInvalid();
        }

        // O índice único de número da parcela exige apagar antes de inserir as novas.
        db.Installments.RemoveRange(payment.Installments);
        await db.SaveChangesAsync(ct);

        var replacements = BuildInstallments(total, dueDates);
        foreach (var installment in replacements)
        {
            installment.PaymentId = payment.Id;
        }

        db.Installments.AddRange(replacements);
    }

    private static void Redistribute(Payment payment, decimal total)
    {
        var ordered = payment.Installments.OrderBy(i => i.InstallmentNumber).ToList();
        var amounts = PaymentRules.SplitAmount(total, ordered.Count);
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].Amount = amounts[index];
        }
    }

    private static List<PaymentInstallment> BuildInstallments(decimal total, IReadOnlyList<DateOnly> dueDates)
    {
        var amounts = PaymentRules.SplitAmount(total, dueDates.Count);
        return dueDates
            .Select((dueDate, index) => new PaymentInstallment { InstallmentNumber = index + 1, Amount = amounts[index], DueDate = dueDate })
            .ToList();
    }

    private async Task ValidateAsync(SavePaymentRequest request, CancellationToken ct)
    {
        new Validator()
            .Required(request.Description, "description", "Descrição")
            .MaxLength(request.Description, 300, "description", "Descrição")
            .Required(request.TotalAmount, "totalAmount", "Valor total")
            .Money(request.TotalAmount, "totalAmount", "Valor total")
            .Required(request.PaymentMethod, "paymentMethod", "Forma de pagamento")
            .ThrowIfInvalid();

        if (request.SupplierId is { } supplierId && !await suppliers.ExistsAsync(supplierId, ct))
        {
            throw new BusinessRuleException("Fornecedor não encontrado neste casamento.");
        }

        if (request.BudgetItemId is { } itemId && !await budget.ItemExistsAsync(itemId, ct))
        {
            throw new BusinessRuleException("Item de orçamento não encontrado neste casamento.");
        }
    }

    private static void Apply(Payment payment, SavePaymentRequest request)
    {
        payment.PaymentMethod = request.PaymentMethod!.Value;
        payment.SupplierId = request.SupplierId;
        payment.BudgetItemId = request.BudgetItemId;
        payment.Notes = Text.Clean(request.Notes);
    }

    private static PaymentTotals ToTotals(IReadOnlyList<Payment> payments, DateOnly today)
    {
        var total = payments.Sum(p => p.TotalAmount);
        var paid = payments.Sum(p => p.PaidAmount);
        var overdue = payments.SelectMany(p => p.Installments).Where(i => i.StatusOn(today) == PaymentStatus.Overdue).ToList();
        return new PaymentTotals(
            total,
            paid,
            payments.Sum(p => p.RemainingAmount),
            overdue.Sum(i => i.RemainingAmount),
            overdue.Count,
            Text.Percent(paid, total));
    }

    private async Task<IReadOnlyList<PaymentResponse>> ToResponsesAsync(IReadOnlyList<Payment> payments, CancellationToken ct)
    {
        var supplierIds = payments.Where(p => p.SupplierId is not null).Select(p => p.SupplierId!.Value).Distinct().ToList();
        var itemIds = payments.Where(p => p.BudgetItemId is not null).Select(p => p.BudgetItemId!.Value).Distinct().ToList();
        var supplierNames = await suppliers.GetNamesAsync(supplierIds, ct);
        var itemNames = await budget.GetItemNamesAsync(itemIds, ct);
        var today = clock.Today;

        return payments.Select(p => new PaymentResponse(
            p.Id,
            p.Description,
            p.TotalAmount,
            p.PaymentMethod,
            p.SupplierId,
            p.SupplierId is { } supplierId ? supplierNames.GetValueOrDefault(supplierId) : null,
            p.BudgetItemId,
            p.BudgetItemId is { } itemId ? itemNames.GetValueOrDefault(itemId) : null,
            p.Notes,
            p.PaidAmount,
            p.RemainingAmount,
            p.StatusOn(today),
            p.Installments.OrderBy(i => i.InstallmentNumber).Select(i => ToResponse(i, today)).ToList())).ToList();
    }

    private static InstallmentResponse ToResponse(PaymentInstallment i, DateOnly today)
    {
        var latest = i.LatestSettlement;
        return new InstallmentResponse(
            i.Id,
            i.InstallmentNumber,
            i.Amount,
            i.DueDate,
            latest?.PaidAt,
            i.PaidAmount,
            i.RemainingAmount,
            latest?.PaymentMethod,
            latest?.Notes,
            i.Receipts.OrderBy(r => r.UploadedAt).Select(ToResponse).ToList(),
            i.StatusOn(today));
    }

    private static ReceiptResponse ToResponse(PaymentReceipt r) =>
        new(r.Id, r.InstallmentId, r.TransactionId, r.FileName, r.ContentType, r.Size, r.UploadedAt);

    private static TransactionResponse ToResponse(InstallmentTransaction t) =>
        new(t.Id, t.Type, t.Amount, t.PaidAt, t.PaymentMethod, t.Notes, t.CreatedByUserId, t.CreatedAt);
}
