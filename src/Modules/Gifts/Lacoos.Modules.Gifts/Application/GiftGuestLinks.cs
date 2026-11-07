using Lacoos.Modules.Gifts.Domain;
using Lacoos.Modules.Gifts.Persistence;
using Lacoos.SharedKernel.Modules;

namespace Lacoos.Modules.Gifts.Application;

internal sealed class GiftGuestLinks(GiftsDbContext db) : IGiftGuestLinks
{
    public async Task DetachGuestAsync(Guid guestId, string guestName, CancellationToken ct)
    {
        await db.Reservations.Where(r => r.GuestId == guestId)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.PersonName, guestName).SetProperty(r => r.GuestId, (Guid?)null), ct);
        await db.Receipts.Where(r => r.GuestId == guestId)
            .ExecuteUpdateAsync(set => set.SetProperty(r => r.PersonName, guestName).SetProperty(r => r.GuestId, (Guid?)null), ct);
    }
}

internal sealed class GiftsSeeder(GiftsDbContext db, WeddingContext weddingContext, IClock clock) : IModuleSeeder
{
    public int Order => 80;

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken ct)
    {
        weddingContext.Set(DemoIds.Wedding, null);
        if (await db.Categories.AnyAsync(ct))
        {
            return;
        }

        var kitchen = new GiftCategory { Name = "Cozinha" };
        var home = new GiftCategory { Name = "Casa" };
        db.Categories.AddRange(kitchen, home);

        var today = clock.Today;
        var mixer = new Gift { Name = "Batedeira", Category = kitchen, EstimatedValue = 450, DesiredQuantity = 1 };
        var plates = new Gift { Name = "Jogo de pratos", Category = kitchen, EstimatedValue = 120, DesiredQuantity = 4 };
        var towels = new Gift { Name = "Jogo de toalhas", Category = home, EstimatedValue = 180, DesiredQuantity = 2 };
        var vase = new Gift { Name = "Vaso decorativo", Category = home, EstimatedValue = 90, Status = GiftStatus.Cancelled };

        var mixerReservation = new GiftReservation { WeddingId = DemoIds.Wedding, Quantity = 1, GuestId = DemoIds.FirstGuest, ReservedAt = today.AddDays(-3) };
        mixer.Reservations.Add(mixerReservation);

        var platesReservation = new GiftReservation { Id = Guid.CreateVersion7(), WeddingId = DemoIds.Wedding, Quantity = 2, GuestId = DemoIds.SecondGuest, ReservedAt = today.AddDays(-10) };
        plates.Reservations.Add(platesReservation);
        plates.Receipts.Add(new GiftReceipt { WeddingId = DemoIds.Wedding, Quantity = 1, GuestId = DemoIds.SecondGuest, ReceivedAt = today.AddDays(-1), ReservationId = platesReservation.Id });

        towels.Receipts.Add(new GiftReceipt { WeddingId = DemoIds.Wedding, Quantity = 1, PersonName = "Tia Lúcia", ReceivedAt = today.AddDays(-2) });

        db.Gifts.AddRange(mixer, plates, towels, vase);
        await db.SaveChangesAsync(ct);
    }
}
