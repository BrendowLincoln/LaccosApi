namespace Lacoos.Modules.Suppliers.Contracts;

public interface ISupplierQueries
{
    Task<bool> ExistsAsync(Guid supplierId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> supplierIds, CancellationToken ct);

    Task<IReadOnlyList<Guid>> SearchIdsAsync(string search, CancellationToken ct);
}
