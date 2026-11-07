using Lacoos.Modules.Identity.Domain;
using Lacoos.Modules.Identity.Persistence;

namespace Lacoos.Modules.Identity.Application;

internal sealed class UserDirectory(IdentityDbContext db) : IUserDirectory
{
    public Task<UserInfo?> FindAsync(Guid userId, CancellationToken ct) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserInfo(u.Id, u.Name, u.Email))
            .FirstOrDefaultAsync(ct);

    public Task<UserInfo?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = User.Normalize(email);
        return db.Users.AsNoTracking()
            .Where(u => u.NormalizedEmail == normalized)
            .Select(u => new UserInfo(u.Id, u.Name, u.Email))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, UserInfo>> GetManyAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new UserInfo(u.Id, u.Name, u.Email))
            .ToDictionaryAsync(u => u.Id, ct);
}
