using Lacoos.Modules.Payments.Domain;
using Lacoos.Modules.Payments.Persistence;
using Lacoos.SharedKernel.Modules;

namespace Lacoos.Modules.Payments.Application;

internal sealed class PaymentQueries(PaymentsDbContext db, IClock clock) : IPaymentQueries
{
    public Task<bool> HasPaymentsForSupplierAsync(Guid supplierId, CancellationToken ct) =>
        db.Payments.AnyAsync(p => p.SupplierId == supplierId, ct);

    public Task<bool> HasPaymentsForBudgetItemAsync(Guid budgetItemId, CancellationToken ct) =>
        db.Payments.AnyAsync(p => p.BudgetItemId == budgetItemId, ct);

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetPaidByBudgetItemAsync(CancellationToken ct)
    {
        var payments = await LoadAsync(ct);
        return payments.Where(p => p.BudgetItemId is not null)
            .GroupBy(p => p.BudgetItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaidAmount));
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetPaidBySupplierAsync(CancellationToken ct)
    {
        var payments = await LoadAsync(ct);
        return payments.Where(p => p.SupplierId is not null)
            .GroupBy(p => p.SupplierId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.PaidAmount));
    }

    public async Task<IReadOnlyList<SupplierPaymentSummary>> GetBySupplierAsync(Guid supplierId, CancellationToken ct)
    {
        var today = clock.Today;
        var payments = await LoadAsync(ct);
        return payments.Where(p => p.SupplierId == supplierId)
            .OrderBy(p => p.Description)
            .Select(p => new SupplierPaymentSummary(
                p.Id, p.Description, p.TotalAmount, p.PaidAmount, p.RemainingAmount, EnumText.ToText(p.StatusOn(today))))
            .ToList();
    }

    public async Task<PaymentsOverview> GetOverviewAsync(int upcomingLimit, CancellationToken ct)
    {
        var today = clock.Today;
        var payments = await LoadAsync(ct);

        var installments = payments
            .SelectMany(p => p.Installments.Select(i => (Payment: p, Installment: i, Status: i.StatusOn(today))))
            .OrderBy(x => x.Installment.DueDate)
            .ThenBy(x => x.Installment.InstallmentNumber)
            .ToList();

        return new PaymentsOverview(
            payments.Sum(p => p.PaidAmount),
            installments
                .Where(x => x.Status is PaymentStatus.Pending or PaymentStatus.Partial)
                .Take(upcomingLimit)
                .Select(ToOpenInstallment)
                .ToList(),
            installments
                .Where(x => x.Status == PaymentStatus.Overdue)
                .Select(ToOpenInstallment)
                .ToList());
    }

    private Task<List<Payment>> LoadAsync(CancellationToken ct) =>
        db.Payments.AsNoTracking()
            .Include(p => p.Installments).ThenInclude(i => i.Transactions)
            .AsSplitQuery()
            .ToListAsync(ct);

    private static OpenInstallment ToOpenInstallment((Payment Payment, PaymentInstallment Installment, PaymentStatus Status) x) => new(
        x.Installment.Id,
        x.Payment.Id,
        x.Payment.Description,
        x.Installment.InstallmentNumber,
        x.Installment.DueDate,
        x.Installment.Amount,
        x.Installment.RemainingAmount,
        EnumText.ToText(x.Status),
        x.Payment.SupplierId);
}

internal sealed class PaymentsSeeder(PaymentsDbContext db, WeddingContext weddingContext, IClock clock) : IModuleSeeder
{
    public int Order => 70;

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken ct)
    {
        weddingContext.Set(DemoIds.Wedding, null);
        if (await db.Payments.AnyAsync(ct))
        {
            return;
        }

        var today = clock.Today;

        var photo = NewPayment("Fotografia completa", 6800, PaymentMethod.Pix, DemoIds.PhotoSupplier, DemoIds.PhotoBudgetItem, today.AddMonths(-2), 4);
        Settle(photo.Installments[0], photo.Installments[0].Amount, today.AddMonths(-2));
        Settle(photo.Installments[1], 500, today.AddMonths(-1));

        var venue = NewPayment("Locação do sítio", 16500, PaymentMethod.Boleto, DemoIds.VenueSupplier, DemoIds.VenueBudgetItem, today.AddDays(10), 6);

        var dress = NewPayment("Sinal do vestido", 1500, PaymentMethod.CreditCard, null, null, today.AddDays(-5), 1);

        db.Payments.AddRange(photo, venue, dress);
        await db.SaveChangesAsync(ct);

        void Settle(PaymentInstallment installment, decimal amount, DateOnly paidAt) =>
            installment.Transactions.Add(new InstallmentTransaction
            {
                WeddingId = DemoIds.Wedding,
                Type = TransactionType.Settlement,
                Amount = amount,
                PaidAt = paidAt,
                PaymentMethod = PaymentMethod.Pix,
                CreatedByUserId = DemoIds.OwnerUser,
                CreatedAt = clock.UtcNow,
            });
    }

    private static Payment NewPayment(
        string description, decimal total, PaymentMethod method, Guid? supplierId, Guid? budgetItemId, DateOnly firstDueDate, int count)
    {
        var amounts = PaymentRules.SplitAmount(total, count);
        var dueDates = PaymentRules.MonthlyDueDates(firstDueDate, count);
        return new Payment
        {
            Description = description,
            TotalAmount = total,
            PaymentMethod = method,
            SupplierId = supplierId,
            BudgetItemId = budgetItemId,
            Installments = Enumerable.Range(0, count)
                .Select(index => new PaymentInstallment { WeddingId = DemoIds.Wedding, InstallmentNumber = index + 1, Amount = amounts[index], DueDate = dueDates[index] })
                .ToList(),
        };
    }
}
