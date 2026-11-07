using Lacoos.Modules.Identity.Domain;
using Lacoos.Modules.Identity.Persistence;
using Lacoos.SharedKernel.Modules;
using Microsoft.AspNetCore.Identity;

namespace Lacoos.Modules.Identity.Application;

internal sealed class IdentitySeeder(IdentityDbContext db, IPasswordHasher<User> hasher, IClock clock) : IModuleSeeder
{
    public int Order => 10;

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.Id == DemoIds.OwnerUser, ct))
        {
            return;
        }

        (Guid Id, string Name, string Email)[] users =
        [
            (DemoIds.OwnerUser, "Milena Demo", "milena@lacoos.local"),
            (DemoIds.OrganizerUser, "Olivia Organizadora", "organizadora@lacoos.local"),
            (DemoIds.ViewerUser, "Vitor Visitante", "visitante@lacoos.local"),
        ];

        foreach (var (id, name, email) in users)
        {
            var user = new User { Id = id, Name = name, Email = email, NormalizedEmail = email, CreatedAt = clock.UtcNow };
            user.PasswordHash = hasher.HashPassword(user, options.DemoPassword);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(ct);
    }
}
