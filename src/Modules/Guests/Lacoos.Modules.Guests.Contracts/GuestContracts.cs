namespace Lacoos.Modules.Guests.Contracts;

public sealed record GuestTotals(int Total, int Confirmed, int Pending, int Declined);

public interface IGuestQueries
{
    Task<bool> ExistsAsync(Guid guestId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> guestIds, CancellationToken ct);

    Task<GuestTotals> GetTotalsAsync(CancellationToken ct);
}
