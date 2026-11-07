namespace Lacoos.Modules.Weddings.Contracts;

public sealed record WeddingInfo(Guid Id, string Name, DateOnly WeddingDate, int EstimatedGuestCount);

/// <summary>Consultas do casamento atual da requisição.</summary>
public interface IWeddingDirectory
{
    Task<WeddingInfo> GetCurrentAsync(CancellationToken ct);

    Task<bool> MemberExistsAsync(Guid memberId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, string>> GetMemberNamesAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct);
}
