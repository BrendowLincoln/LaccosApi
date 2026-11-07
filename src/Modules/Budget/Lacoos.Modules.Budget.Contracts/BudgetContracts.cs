namespace Lacoos.Modules.Budget.Contracts;

public sealed record BudgetFinancials(decimal TotalBudget, decimal Committed, decimal Balance, decimal CommittedPercent);

public interface IBudgetQueries
{
    Task<bool> ItemExistsAsync(Guid budgetItemId, CancellationToken ct);

    Task<bool> HasItemsForSupplierAsync(Guid supplierId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, string>> GetItemNamesAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct);

    Task<IReadOnlyList<Guid>> SearchItemIdsAsync(string search, CancellationToken ct);

    Task<BudgetFinancials> GetFinancialsAsync(CancellationToken ct);
}
