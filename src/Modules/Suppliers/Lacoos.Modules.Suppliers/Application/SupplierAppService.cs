using Lacoos.Modules.Suppliers.Domain;
using Lacoos.Modules.Suppliers.Persistence;
using Lacoos.SharedKernel.Files;
using Microsoft.AspNetCore.Http;

namespace Lacoos.Modules.Suppliers.Application;

internal sealed record SupplierFilter(string? Search, string? Category, string? NegotiationStatus);

internal sealed record SaveSupplierRequest(
    string? Name,
    string? Category,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Website,
    string? SocialMedia,
    string? Address,
    NegotiationStatus? NegotiationStatus,
    string? Notes,
    string? CoverImage);

internal sealed record SupplierFinancials(decimal Quoted, decimal Contracted, decimal Paid, decimal Open, decimal PaidPercent);

internal sealed record SupplierResponse(
    Guid Id,
    string Name,
    string Category,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Website,
    string? SocialMedia,
    string? Address,
    NegotiationStatus NegotiationStatus,
    string? Notes,
    string? CoverImage,
    SupplierFinancials Financials);

internal sealed record SupplierDetailResponse(
    SupplierResponse Supplier, IReadOnlyList<ServiceResponse> Services, IReadOnlyList<ContractResponse> Contracts);

internal sealed record SupplierTotals(int Total, int Hired, int Negotiating, decimal TotalPaid);

internal sealed record SupplierListResponse(IReadOnlyList<SupplierResponse> Items, SupplierTotals Totals);

internal sealed record SaveServiceRequest(
    string? Name, string? Description, decimal? QuotedAmount, decimal? ContractedAmount, ServiceStatus? Status);

internal sealed record ServiceResponse(
    Guid Id, Guid SupplierId, string Name, string? Description, decimal QuotedAmount, decimal ContractedAmount, ServiceStatus Status);

internal sealed record SaveContractRequest(string? ContractNumber, DateOnly? SignedAt, ContractStatus? Status);

internal sealed record ContractFileResponse(string FileName, string ContentType, long Size);

internal sealed record ContractResponse(
    Guid Id, Guid SupplierId, string ContractNumber, DateOnly? SignedAt, ContractStatus Status, ContractFileResponse? File);

internal sealed class SupplierAppService(
    SuppliersDbContext db,
    IBudgetQueries budget,
    IPaymentQueries payments,
    IFileStorage files,
    IWeddingContext wedding)
{
    public async Task<SupplierListResponse> ListAsync(SupplierFilter filter, CancellationToken ct)
    {
        var status = EnumText.ParseFilter<NegotiationStatus>(filter.NegotiationStatus, "negotiationStatus");
        var query = db.Suppliers.AsNoTracking().Include(s => s.Services).AsQueryable();

        if (Text.Clean(filter.Search) is { } search)
        {
            var pattern = Text.ContainsPattern(search);
            query = query.Where(s =>
                EF.Functions.ILike(s.Name, pattern, @"\")
                || EF.Functions.ILike(s.Category, pattern, @"\")
                || (s.ContactName != null && EF.Functions.ILike(s.ContactName, pattern, @"\"))
                || (s.Email != null && EF.Functions.ILike(s.Email, pattern, @"\"))
                || (s.Phone != null && EF.Functions.ILike(s.Phone, pattern, @"\")));
        }

        if (Text.Clean(filter.Category) is { } category)
        {
            query = query.Where(s => s.Category == category);
        }

        if (status is not null)
        {
            query = query.Where(s => s.NegotiationStatus == status);
        }

        var suppliers = await query.OrderBy(s => s.Name).ToListAsync(ct);
        var paid = await payments.GetPaidBySupplierAsync(ct);
        var all = await db.Suppliers.AsNoTracking().Select(s => s.NegotiationStatus).ToListAsync(ct);

        var totals = new SupplierTotals(
            all.Count,
            all.Count(s => s == NegotiationStatus.Hired),
            all.Count(s => s is NegotiationStatus.Contacted or NegotiationStatus.Quoted or NegotiationStatus.Negotiating),
            paid.Values.Sum());
        return new SupplierListResponse(suppliers.Select(s => ToResponse(s, paid.GetValueOrDefault(s.Id))).ToList(), totals);
    }

    public async Task<SupplierDetailResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking()
            .Include(s => s.Services).Include(s => s.Contracts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Fornecedor não encontrado.");

        var paid = await payments.GetPaidBySupplierAsync(ct);
        return new SupplierDetailResponse(
            ToResponse(supplier, paid.GetValueOrDefault(id)),
            supplier.Services.OrderBy(s => s.Name).Select(ToResponse).ToList(),
            supplier.Contracts.OrderBy(c => c.ContractNumber).Select(ToResponse).ToList());
    }

    public async Task<SupplierDetailResponse> CreateAsync(SaveSupplierRequest request, CancellationToken ct)
    {
        Validate(request);
        var supplier = new Supplier { Name = request.Name!.Trim(), Category = request.Category!.Trim() };
        Apply(supplier, request);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return await GetAsync(supplier.Id, ct);
    }

    public async Task<SupplierDetailResponse> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken ct)
    {
        Validate(request);
        var supplier = await LoadAsync(id, ct);
        supplier.Name = request.Name!.Trim();
        supplier.Category = request.Category!.Trim();
        Apply(supplier, request);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.Include(s => s.Contracts).FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Fornecedor não encontrado.");

        if (await budget.HasItemsForSupplierAsync(id, ct))
        {
            throw new ConflictException("Não é possível excluir um fornecedor com itens de orçamento vinculados.");
        }

        if (await payments.HasPaymentsForSupplierAsync(id, ct))
        {
            throw new ConflictException("Não é possível excluir um fornecedor com pagamentos vinculados.");
        }

        var fileKeys = supplier.Contracts.Where(c => c.FileKey is not null).Select(c => c.FileKey!).ToList();
        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);

        foreach (var key in fileKeys)
        {
            await files.DeleteAsync(key, ct);
        }
    }

    public async Task<IReadOnlyList<SupplierPaymentSummary>> ListPaymentsAsync(Guid id, CancellationToken ct)
    {
        await LoadAsync(id, ct);
        return await payments.GetBySupplierAsync(id, ct);
    }

    public async Task<IReadOnlyList<ServiceResponse>> ListServicesAsync(Guid supplierId, CancellationToken ct)
    {
        await LoadAsync(supplierId, ct);
        var services = await db.Services.AsNoTracking()
            .Where(s => s.SupplierId == supplierId).OrderBy(s => s.Name).ToListAsync(ct);
        return services.Select(ToResponse).ToList();
    }

    public async Task<ServiceResponse> GetServiceAsync(Guid supplierId, Guid id, CancellationToken ct) =>
        ToResponse(await LoadServiceAsync(supplierId, id, ct));

    public async Task<ServiceResponse> CreateServiceAsync(Guid supplierId, SaveServiceRequest request, CancellationToken ct)
    {
        ValidateService(request);
        await LoadAsync(supplierId, ct);
        var service = new SupplierService { SupplierId = supplierId, Name = request.Name!.Trim() };
        ApplyService(service, request);
        db.Services.Add(service);
        await db.SaveChangesAsync(ct);
        return ToResponse(service);
    }

    public async Task<ServiceResponse> UpdateServiceAsync(Guid supplierId, Guid id, SaveServiceRequest request, CancellationToken ct)
    {
        ValidateService(request);
        var service = await LoadServiceAsync(supplierId, id, ct);
        service.Name = request.Name!.Trim();
        ApplyService(service, request);
        await db.SaveChangesAsync(ct);
        return ToResponse(service);
    }

    public async Task DeleteServiceAsync(Guid supplierId, Guid id, CancellationToken ct)
    {
        db.Services.Remove(await LoadServiceAsync(supplierId, id, ct));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ContractResponse>> ListContractsAsync(Guid supplierId, CancellationToken ct)
    {
        await LoadAsync(supplierId, ct);
        var contracts = await db.Contracts.AsNoTracking()
            .Where(c => c.SupplierId == supplierId).OrderBy(c => c.ContractNumber).ToListAsync(ct);
        return contracts.Select(ToResponse).ToList();
    }

    public async Task<ContractResponse> GetContractAsync(Guid supplierId, Guid id, CancellationToken ct) =>
        ToResponse(await LoadContractAsync(supplierId, id, ct));

    public async Task<ContractResponse> CreateContractAsync(Guid supplierId, SaveContractRequest request, CancellationToken ct)
    {
        ValidateContract(request);
        await LoadAsync(supplierId, ct);
        var contract = new SupplierContract
        {
            SupplierId = supplierId,
            ContractNumber = request.ContractNumber!.Trim(),
            SignedAt = request.SignedAt,
            Status = request.Status ?? ContractStatus.Pending,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync(ct);
        return ToResponse(contract);
    }

    public async Task<ContractResponse> UpdateContractAsync(Guid supplierId, Guid id, SaveContractRequest request, CancellationToken ct)
    {
        ValidateContract(request);
        var contract = await LoadContractAsync(supplierId, id, ct);
        contract.ContractNumber = request.ContractNumber!.Trim();
        contract.SignedAt = request.SignedAt;
        contract.Status = request.Status ?? contract.Status;
        await db.SaveChangesAsync(ct);
        return ToResponse(contract);
    }

    public async Task DeleteContractAsync(Guid supplierId, Guid id, CancellationToken ct)
    {
        var contract = await LoadContractAsync(supplierId, id, ct);
        db.Contracts.Remove(contract);
        await db.SaveChangesAsync(ct);
        if (contract.FileKey is not null)
        {
            await files.DeleteAsync(contract.FileKey, ct);
        }
    }

    public async Task<ContractResponse> UploadContractFileAsync(Guid supplierId, Guid id, IFormFile? file, CancellationToken ct)
    {
        var contract = await LoadContractAsync(supplierId, id, ct);
        var stored = await files.StoreAsync(file, "contracts", wedding.WeddingId!.Value, ct);
        var previousKey = contract.FileKey;

        contract.FileKey = stored.Key;
        contract.FileName = stored.FileName;
        contract.FileContentType = stored.ContentType;
        contract.FileSize = stored.Size;
        await db.SaveChangesAsync(ct);

        if (previousKey is not null)
        {
            await files.DeleteAsync(previousKey, ct);
        }

        return ToResponse(contract);
    }

    public async Task<IResult> DownloadContractFileAsync(Guid supplierId, Guid id, CancellationToken ct)
    {
        var contract = await LoadContractAsync(supplierId, id, ct);
        if (contract.FileKey is null)
        {
            throw new NotFoundException("Este contrato não tem arquivo.");
        }

        return await files.DownloadAsync(contract.FileKey, contract.FileName!, contract.FileContentType!, ct);
    }

    private async Task<Supplier> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
        ?? throw new NotFoundException("Fornecedor não encontrado.");

    private async Task<SupplierService> LoadServiceAsync(Guid supplierId, Guid id, CancellationToken ct) =>
        await db.Services.FirstOrDefaultAsync(s => s.Id == id && s.SupplierId == supplierId, ct)
        ?? throw new NotFoundException("Serviço não encontrado.");

    private async Task<SupplierContract> LoadContractAsync(Guid supplierId, Guid id, CancellationToken ct) =>
        await db.Contracts.FirstOrDefaultAsync(c => c.Id == id && c.SupplierId == supplierId, ct)
        ?? throw new NotFoundException("Contrato não encontrado.");

    private static void Validate(SaveSupplierRequest request) =>
        new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .Required(request.Category, "category", "Categoria")
            .MaxLength(request.Category, 100, "category", "Categoria")
            .Email(request.Email, "email")
            .ThrowIfInvalid();

    private static void ValidateService(SaveServiceRequest request) =>
        new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .Money(request.QuotedAmount, "quotedAmount", "Valor cotado")
            .Money(request.ContractedAmount, "contractedAmount", "Valor contratado")
            .ThrowIfInvalid();

    private static void ValidateContract(SaveContractRequest request) =>
        new Validator()
            .Required(request.ContractNumber, "contractNumber", "Número do contrato")
            .MaxLength(request.ContractNumber, 100, "contractNumber", "Número do contrato")
            .ThrowIfInvalid();

    private static void Apply(Supplier supplier, SaveSupplierRequest request)
    {
        supplier.ContactName = Text.Clean(request.ContactName);
        supplier.Email = Text.Clean(request.Email);
        supplier.Phone = Text.Clean(request.Phone);
        supplier.Website = Text.Clean(request.Website);
        supplier.SocialMedia = Text.Clean(request.SocialMedia);
        supplier.Address = Text.Clean(request.Address);
        supplier.NegotiationStatus = request.NegotiationStatus ?? supplier.NegotiationStatus;
        supplier.Notes = Text.Clean(request.Notes);
        supplier.CoverImage = Text.Clean(request.CoverImage);
    }

    private static void ApplyService(SupplierService service, SaveServiceRequest request)
    {
        service.Description = Text.Clean(request.Description);
        service.QuotedAmount = request.QuotedAmount ?? 0;
        service.ContractedAmount = request.ContractedAmount ?? 0;
        service.Status = request.Status ?? service.Status;
    }

    /// <summary>Serviços cancelados não contam em cotado nem em contratado.</summary>
    private static SupplierResponse ToResponse(Supplier s, decimal paid)
    {
        var active = s.Services.Where(v => v.Status != ServiceStatus.Cancelled).ToList();
        var contracted = active.Sum(v => v.ContractedAmount);
        var financials = new SupplierFinancials(
            active.Sum(v => v.QuotedAmount),
            contracted,
            paid,
            Math.Max(contracted - paid, 0),
            Text.Percent(paid, contracted));
        return new SupplierResponse(
            s.Id, s.Name, s.Category, s.ContactName, s.Email, s.Phone, s.Website, s.SocialMedia,
            s.Address, s.NegotiationStatus, s.Notes, s.CoverImage, financials);
    }

    private static ServiceResponse ToResponse(SupplierService s) =>
        new(s.Id, s.SupplierId, s.Name, s.Description, s.QuotedAmount, s.ContractedAmount, s.Status);

    private static ContractResponse ToResponse(SupplierContract c) => new(
        c.Id, c.SupplierId, c.ContractNumber, c.SignedAt, c.Status,
        c.FileKey is null ? null : new ContractFileResponse(c.FileName!, c.FileContentType!, c.FileSize!.Value));
}
