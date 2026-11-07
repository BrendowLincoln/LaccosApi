using Lacoos.Modules.Budget.Domain;
using Lacoos.Modules.Budget.Persistence;

namespace Lacoos.Modules.Budget.Application;

internal sealed record BudgetItemFilter(string? Search, Guid? CategoryId, Guid? SupplierId, string? Status);

internal sealed record SetTotalBudgetRequest(decimal? TotalBudget);

internal sealed record BudgetTotals(
    decimal Planned, decimal Contracted, decimal Paid, decimal Pending, decimal Balance, decimal CommittedPercent);

internal sealed record BudgetOverviewResponse(decimal TotalBudget, bool TotalBudgetDefined, BudgetTotals Totals);

internal sealed record SaveCategoryRequest(string? Name, string? Description, decimal? PlannedAmount);

internal sealed record CategoryResponse(Guid Id, string Name, string? Description, decimal PlannedAmount);

internal sealed record CategoryChartRow(Guid CategoryId, string Name, decimal Planned, decimal Contracted);

internal sealed record SaveItemRequest(
    string? Name,
    string? Description,
    Guid? CategoryId,
    Guid? SupplierId,
    decimal? PlannedAmount,
    decimal? ContractedAmount,
    BudgetItemStatus? Status);

internal sealed record ItemResponse(
    Guid Id,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    Guid? SupplierId,
    string? SupplierName,
    decimal PlannedAmount,
    decimal ContractedAmount,
    decimal PaidAmount,
    BudgetItemStatus Status);

internal sealed class BudgetService(
    BudgetDbContext db,
    ISupplierQueries suppliers,
    IPaymentQueries payments,
    IWeddingContext wedding)
{
    public async Task<BudgetOverviewResponse> GetOverviewAsync(CancellationToken ct)
    {
        var defined = await db.Settings.AsNoTracking().Select(s => (decimal?)s.TotalAmount).FirstOrDefaultAsync(ct);
        var planned = await db.Categories.SumAsync(c => c.PlannedAmount, ct);
        var items = await db.Items.AsNoTracking().ToListAsync(ct);
        var paidByItem = await payments.GetPaidByBudgetItemAsync(ct);

        var totalBudget = BudgetMath.TotalBudget(defined, planned);
        var contracted = items.Sum(i => i.CommittedAmount);
        var paid = items.Sum(i => paidByItem.GetValueOrDefault(i.Id));
        var pending = items
            .Where(i => i.Status != BudgetItemStatus.Cancelled)
            .Sum(i => Math.Max(i.ContractedAmount - paidByItem.GetValueOrDefault(i.Id), 0));

        return new BudgetOverviewResponse(
            totalBudget,
            defined is not null,
            new BudgetTotals(planned, contracted, paid, pending, totalBudget - contracted, Text.Percent(contracted, totalBudget)));
    }

    public async Task<BudgetOverviewResponse> SetTotalAsync(SetTotalBudgetRequest request, CancellationToken ct)
    {
        new Validator()
            .Required(request.TotalBudget, "totalBudget", "Orçamento total")
            .Money(request.TotalBudget, "totalBudget", "Orçamento total")
            .ThrowIfInvalid();

        var settings = await db.Settings.FirstOrDefaultAsync(ct);
        if (settings is null)
        {
            db.Settings.Add(new BudgetSettings { WeddingId = wedding.WeddingId!.Value, TotalAmount = request.TotalBudget!.Value });
        }
        else
        {
            settings.TotalAmount = request.TotalBudget!.Value;
        }

        await db.SaveChangesAsync(ct);
        return await GetOverviewAsync(ct);
    }

    public async Task<IReadOnlyList<CategoryChartRow>> GetByCategoryAsync(CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        var items = await db.Items.AsNoTracking().ToListAsync(ct);
        return categories
            .Select(c => new CategoryChartRow(c.Id, c.Name, c.PlannedAmount, items.Where(i => i.CategoryId == c.Id).Sum(i => i.CommittedAmount)))
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryResponse>> ListCategoriesAsync(CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        return categories.Select(ToResponse).ToList();
    }

    public async Task<CategoryResponse> GetCategoryAsync(Guid id, CancellationToken ct) =>
        ToResponse(await LoadCategoryAsync(id, ct));

    public async Task<CategoryResponse> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        ValidateCategory(request);
        var category = new BudgetCategory
        {
            Name = request.Name!.Trim(),
            Description = Text.Clean(request.Description),
            PlannedAmount = request.PlannedAmount ?? 0,
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task<CategoryResponse> UpdateCategoryAsync(Guid id, SaveCategoryRequest request, CancellationToken ct)
    {
        ValidateCategory(request);
        var category = await LoadCategoryAsync(id, ct);
        category.Name = request.Name!.Trim();
        category.Description = Text.Clean(request.Description);
        category.PlannedAmount = request.PlannedAmount ?? 0;
        await db.SaveChangesAsync(ct);
        return ToResponse(category);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct)
    {
        var category = await LoadCategoryAsync(id, ct);
        if (await db.Items.AnyAsync(i => i.CategoryId == id, ct))
        {
            throw new ConflictException("Não é possível excluir uma categoria que tem itens.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ItemResponse>> ListItemsAsync(BudgetItemFilter filter, CancellationToken ct)
    {
        var status = EnumText.ParseFilter<BudgetItemStatus>(filter.Status, "status");
        var query = db.Items.AsNoTracking().Include(i => i.Category).AsQueryable();

        if (Text.Clean(filter.Search) is { } search)
        {
            var pattern = Text.ContainsPattern(search);
            query = query.Where(i => EF.Functions.ILike(i.Name, pattern, @"\")
                || (i.Description != null && EF.Functions.ILike(i.Description, pattern, @"\")));
        }

        if (filter.CategoryId is not null)
        {
            query = query.Where(i => i.CategoryId == filter.CategoryId);
        }

        if (filter.SupplierId is not null)
        {
            query = query.Where(i => i.SupplierId == filter.SupplierId);
        }

        if (status is not null)
        {
            query = query.Where(i => i.Status == status);
        }

        return await ToResponsesAsync(await query.OrderBy(i => i.Name).ToListAsync(ct), ct);
    }

    public async Task<ItemResponse> GetItemAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Items.AsNoTracking().Include(i => i.Category).FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Item de orçamento não encontrado.");
        return (await ToResponsesAsync([item], ct))[0];
    }

    public async Task<ItemResponse> CreateItemAsync(SaveItemRequest request, CancellationToken ct)
    {
        await ValidateItemAsync(request, ct);
        var item = new BudgetItem { Name = request.Name!.Trim() };
        Apply(item, request);
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);
        return await GetItemAsync(item.Id, ct);
    }

    public async Task<ItemResponse> UpdateItemAsync(Guid id, SaveItemRequest request, CancellationToken ct)
    {
        await ValidateItemAsync(request, ct);
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Item de orçamento não encontrado.");
        item.Name = request.Name!.Trim();
        Apply(item, request);
        await db.SaveChangesAsync(ct);
        return await GetItemAsync(id, ct);
    }

    public async Task DeleteItemAsync(Guid id, CancellationToken ct)
    {
        var item = await db.Items.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Item de orçamento não encontrado.");
        if (await payments.HasPaymentsForBudgetItemAsync(id, ct))
        {
            throw new ConflictException("Não é possível excluir um item com pagamentos vinculados.");
        }

        db.Items.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    private async Task<BudgetCategory> LoadCategoryAsync(Guid id, CancellationToken ct) =>
        await db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
        ?? throw new NotFoundException("Categoria não encontrada.");

    private async Task ValidateItemAsync(SaveItemRequest request, CancellationToken ct)
    {
        new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .Required(request.CategoryId, "categoryId", "Categoria")
            .Money(request.PlannedAmount, "plannedAmount", "Valor planejado")
            .Money(request.ContractedAmount, "contractedAmount", "Valor contratado")
            .ThrowIfInvalid();

        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw new BusinessRuleException("Categoria não encontrada neste casamento.");
        }

        if (request.SupplierId is { } supplierId && !await suppliers.ExistsAsync(supplierId, ct))
        {
            throw new BusinessRuleException("Fornecedor não encontrado neste casamento.");
        }
    }

    private static void ValidateCategory(SaveCategoryRequest request) =>
        new Validator()
            .Required(request.Name, "name", "Nome")
            .MaxLength(request.Name, 200, "name", "Nome")
            .Money(request.PlannedAmount, "plannedAmount", "Valor planejado")
            .ThrowIfInvalid();

    private static void Apply(BudgetItem item, SaveItemRequest request)
    {
        item.Description = Text.Clean(request.Description);
        item.CategoryId = request.CategoryId!.Value;
        item.SupplierId = request.SupplierId;
        item.PlannedAmount = request.PlannedAmount ?? 0;
        item.ContractedAmount = request.ContractedAmount ?? 0;
        item.Status = request.Status ?? item.Status;
    }

    private async Task<IReadOnlyList<ItemResponse>> ToResponsesAsync(IReadOnlyList<BudgetItem> items, CancellationToken ct)
    {
        var supplierIds = items.Where(i => i.SupplierId is not null).Select(i => i.SupplierId!.Value).Distinct().ToList();
        var names = await suppliers.GetNamesAsync(supplierIds, ct);
        var paid = await payments.GetPaidByBudgetItemAsync(ct);
        return items.Select(i => new ItemResponse(
            i.Id, i.Name, i.Description, i.CategoryId, i.Category!.Name, i.SupplierId,
            i.SupplierId is { } id ? names.GetValueOrDefault(id) : null,
            i.PlannedAmount, i.ContractedAmount, paid.GetValueOrDefault(i.Id), i.Status)).ToList();
    }

    private static CategoryResponse ToResponse(BudgetCategory c) => new(c.Id, c.Name, c.Description, c.PlannedAmount);
}
