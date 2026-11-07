using Lacoos.Modules.Guests.Domain;
using Lacoos.Modules.Guests.Persistence;
using Lacoos.SharedKernel.Modules;

namespace Lacoos.Modules.Guests.Application;

internal sealed class GuestQueries(GuestsDbContext db) : IGuestQueries
{
    public Task<bool> ExistsAsync(Guid guestId, CancellationToken ct) =>
        db.Guests.AnyAsync(g => g.Id == guestId, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> guestIds, CancellationToken ct) =>
        await db.Guests.AsNoTracking()
            .Where(g => guestIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.FirstName + " " + g.LastName, ct);

    public async Task<GuestTotals> GetTotalsAsync(CancellationToken ct)
    {
        var counts = await db.Guests.AsNoTracking()
            .GroupBy(g => g.AttendanceStatus)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);
        return new GuestTotals(
            counts.Values.Sum(),
            counts.GetValueOrDefault(AttendanceStatus.Confirmed),
            counts.GetValueOrDefault(AttendanceStatus.Pending),
            counts.GetValueOrDefault(AttendanceStatus.Declined));
    }
}

internal sealed class GuestsSeeder(GuestsDbContext db, WeddingContext weddingContext) : IModuleSeeder
{
    public int Order => 40;

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken ct)
    {
        weddingContext.Set(DemoIds.Wedding, null);
        if (await db.Groups.AnyAsync(ct))
        {
            return;
        }

        var family = new GuestGroup { Name = "Família da noiva", Category = GroupCategory.Family };
        var friends = new GuestGroup { Name = "Amigos da faculdade", Category = GroupCategory.Friends };
        var work = new GuestGroup { Name = "Colegas de trabalho", Category = GroupCategory.Work };
        db.Groups.AddRange(family, friends, work);

        db.Guests.AddRange(
            new Guest { Id = DemoIds.FirstGuest, FirstName = "Ana", LastName = "Souza", Group = family, Email = "ana@example.com", Phone = "31999990001", MaxCompanions = 1, InvitationStatus = InvitationStatus.Opened, AttendanceStatus = AttendanceStatus.Confirmed, DietaryRestrictions = "Vegetariana" },
            new Guest { Id = DemoIds.SecondGuest, FirstName = "Bruno", LastName = "Lima", Group = friends, Email = "bruno@example.com", MaxCompanions = 2, InvitationStatus = InvitationStatus.Sent },
            new Guest { FirstName = "Carla", LastName = "Mendes", Group = family, Phone = "31999990003", InvitationStatus = InvitationStatus.Sent, AttendanceStatus = AttendanceStatus.Confirmed, AccessibilityNeeds = "Cadeirante" },
            new Guest { FirstName = "Diego", LastName = "Rocha", Group = work, InvitationStatus = InvitationStatus.Sent, AttendanceStatus = AttendanceStatus.Declined },
            new Guest { FirstName = "Elisa", LastName = "Prado", Group = friends, MaxCompanions = 1 });
        await db.SaveChangesAsync(ct);
    }
}
