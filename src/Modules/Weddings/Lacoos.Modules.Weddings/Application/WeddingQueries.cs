using Lacoos.Modules.Weddings.Domain;
using Lacoos.Modules.Weddings.Persistence;

namespace Lacoos.Modules.Weddings.Application;

internal sealed class MembershipResolver(WeddingsDbContext db) : IWeddingMembershipResolver
{
    public async Task<WeddingRole?> GetActiveRoleAsync(Guid weddingId, Guid userId, CancellationToken ct) =>
        await db.Members.AsNoTracking()
            .Where(m => m.WeddingId == weddingId && m.UserId == userId && m.Status == MemberStatus.Active)
            .Select(m => (WeddingRole?)m.Role)
            .FirstOrDefaultAsync(ct);
}

internal sealed class WeddingDirectory(WeddingsDbContext db, IWeddingContext context, IUserDirectory users) : IWeddingDirectory
{
    public async Task<WeddingInfo> GetCurrentAsync(CancellationToken ct) =>
        await db.Weddings.AsNoTracking()
            .Where(w => w.Id == context.WeddingId)
            .Select(w => new WeddingInfo(w.Id, w.Name, w.WeddingDate, w.EstimatedGuestCount))
            .FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Casamento não encontrado.");

    public Task<bool> MemberExistsAsync(Guid memberId, CancellationToken ct) =>
        db.Members.AnyAsync(m => m.Id == memberId && m.WeddingId == context.WeddingId, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetMemberNamesAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct)
    {
        var members = await db.Members.AsNoTracking()
            .Where(m => m.WeddingId == context.WeddingId && memberIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Name, m.UserId })
            .ToListAsync(ct);
        var userIds = members.Where(m => m.UserId is not null).Select(m => m.UserId!.Value).ToList();
        var profiles = await users.GetManyAsync(userIds, ct);
        return members.ToDictionary(
            m => m.Id,
            m => m.UserId is { } id && profiles.TryGetValue(id, out var profile) ? profile.Name : m.Name);
    }
}
