using Lacoos.Modules.Weddings.Domain;
using Lacoos.Modules.Weddings.Persistence;
using Lacoos.SharedKernel.Modules;

namespace Lacoos.Modules.Weddings.Application;

internal sealed class WeddingsSeeder(WeddingsDbContext db, IClock clock) : IModuleSeeder
{
    public int Order => 20;

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken ct)
    {
        if (await db.Weddings.AnyAsync(w => w.Id == DemoIds.Wedding, ct))
        {
            return;
        }

        db.Weddings.Add(new Wedding
        {
            Id = DemoIds.Wedding,
            Name = "Casamento de Milena e Brendow",
            WeddingDate = clock.Today.AddMonths(8),
            EstimatedGuestCount = 120,
            Partner1 = "Milena",
            Partner2 = "Brendow",
            Time = new TimeOnly(16, 30),
            City = "Belo Horizonte",
            State = "MG",
            Venue = "Sítio das Palmeiras",
        });

        (Guid Id, Guid UserId, string Name, string Email, WeddingRole Role)[] members =
        [
            (DemoIds.OwnerMember, DemoIds.OwnerUser, "Milena Demo", "milena@lacoos.local", WeddingRole.Owner),
            (DemoIds.OrganizerMember, DemoIds.OrganizerUser, "Olivia Organizadora", "organizadora@lacoos.local", WeddingRole.Organizer),
            (DemoIds.ViewerMember, DemoIds.ViewerUser, "Vitor Visitante", "visitante@lacoos.local", WeddingRole.Viewer),
        ];

        foreach (var (id, userId, name, email, role) in members)
        {
            db.Members.Add(new WeddingMember
            {
                Id = id,
                WeddingId = DemoIds.Wedding,
                UserId = userId,
                Name = name,
                Email = email,
                NormalizedEmail = email,
                Role = role,
                Status = MemberStatus.Active,
                JoinedAt = clock.Today,
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
