using System.Net;
using Lacoos.IntegrationTests.Support;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class GiftsTests(ApiFixture api) : ApiTest(api)
{
    private static async Task<(Guid Category, Guid Gift)> NewGift(TestUser owner, string w, int desired = 3, decimal value = 100, string name = "Batedeira")
    {
        var category = (await owner.Post($"{w}/gift-categories", new { name = "Cozinha" })).Json.Id();
        var gift = await owner.Post($"{w}/gifts", new { name, categoryId = category, desiredQuantity = desired, estimatedValue = value });
        return (category, gift.Json.Id("gift.id"));
    }

    private static async Task<int> Available(TestUser owner, string w, Guid gift) =>
        (await owner.Get($"{w}/gifts/{gift}")).Json.Int("gift.availableUnits");

    [Fact]
    public async Task Name_is_required_and_desired_quantity_is_at_least_1()
    {
        var (owner, w, _) = await NewOwner();
        var category = (await owner.Post($"{w}/gift-categories", new { name = "Casa" })).Json.Id();

        var noName = await owner.Post($"{w}/gifts", new { categoryId = category, desiredQuantity = 1 });
        var zero = await owner.Post($"{w}/gifts", new { name = "X", categoryId = category, desiredQuantity = 0 });
        var script = await owner.Post($"{w}/gifts", new { name = "X", categoryId = category, desiredQuantity = 1, externalLink = "javascript:alert(1)" });

        Assert.Equal("Nome é obrigatório.", noName.Json.Str("errors.name.0"));
        Assert.Equal(HttpStatusCode.BadRequest, zero.Status);
        Assert.Equal(HttpStatusCode.BadRequest, script.Status);
    }

    [Fact]
    public async Task Reservation_cannot_exceed_available_units()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w, 3);

        var first = await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 2, personName = "Tia" });
        var excess = await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 2, personName = "Tio" });
        var exact = await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1, personName = "Tio" });

        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, excess.Status);
        Assert.Equal(HttpStatusCode.Created, exact.Status);
        Assert.Equal(0, await Available(owner, w, gift));
    }

    [Fact]
    public async Task Concurrent_reservations_cannot_oversell()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w, 3);

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1, personName = $"Pessoa {i}" })));

        Assert.Equal(3, responses.Count(r => r.Status == HttpStatusCode.Created));
        Assert.Equal(3, (await owner.Get($"{w}/gifts/{gift}")).Json.Int("gift.reservedUnits"));
    }

    [Fact]
    public async Task Cancelling_a_reservation_returns_units_and_cannot_repeat()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w, 3);
        var reservation = (await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 2, personName = "Tia" })).Json.Id();

        var cancelled = await owner.Post($"{w}/gift-reservations/{reservation}/cancel");
        var again = await owner.Post($"{w}/gift-reservations/{reservation}/cancel");

        Assert.Equal("cancelled", cancelled.Json.Str("status"));
        Assert.Equal(3, await Available(owner, w, gift));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, again.Status);
    }

    [Fact]
    public async Task Receiving_a_reservation_consumes_it_without_counting_twice()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w, 3);
        var reservation = (await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 2, personName = "Tia" })).Json.Id();

        var partial = await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, reservationId = reservation });
        var afterPartial = await owner.Get($"{w}/gifts/{gift}");
        var excess = await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 2, reservationId = reservation });
        await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, reservationId = reservation });
        var afterAll = await owner.Get($"{w}/gifts/{gift}");
        var cancelConsumed = await owner.Post($"{w}/gift-reservations/{reservation}/cancel");

        Assert.Equal("Tia", partial.Json.Str("personName"));
        Assert.Equal(1, afterPartial.Json.Int("gift.reservedUnits"));
        Assert.Equal(1, afterPartial.Json.Int("gift.receivedUnits"));
        Assert.Equal(1, afterPartial.Json.Int("gift.availableUnits"));
        Assert.Equal(1, afterPartial.Json.Int("reservations.0.remainingQuantity"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, excess.Status);
        Assert.Equal(0, afterAll.Json.Int("gift.reservedUnits"));
        Assert.Equal(2, afterAll.Json.Int("gift.receivedUnits"));
        Assert.Equal(1, afterAll.Json.Int("gift.availableUnits"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, cancelConsumed.Status);
    }

    [Fact]
    public async Task Cancelling_a_partly_received_reservation_only_returns_its_balance()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w, 3);
        var reservation = (await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 3, personName = "Tia" })).Json.Id();
        await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, reservationId = reservation });

        await owner.Post($"{w}/gift-reservations/{reservation}/cancel");
        var detail = await owner.Get($"{w}/gifts/{gift}");

        Assert.Equal(1, detail.Json.Int("gift.receivedUnits"));
        Assert.Equal(2, detail.Json.Int("gift.availableUnits"));
    }

    [Fact]
    public async Task Receipt_without_reservation_cannot_exceed_available()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w, 3);
        await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 2, personName = "Tia" });

        var excess = await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 2, personName = "Tio" });
        var exact = await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, personName = "Tio" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, excess.Status);
        Assert.Equal(HttpStatusCode.Created, exact.Status);
    }

    [Fact]
    public async Task Receipt_cannot_use_a_cancelled_reservation_or_one_from_another_gift()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w, 3);
        var (_, other) = await NewGift(owner, w, 3, name: "Outro");
        var cancelled = (await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1, personName = "Tia" })).Json.Id();
        await owner.Post($"{w}/gift-reservations/{cancelled}/cancel");
        var active = (await owner.Post($"{w}/gifts/{other}/reservations", new { quantity = 1, personName = "Tio" })).Json.Id();

        var fromCancelled = await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, reservationId = cancelled });
        var fromOtherGift = await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, reservationId = active });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, fromCancelled.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, fromOtherGift.Status);
    }

    [Fact]
    public async Task Reservation_needs_exactly_one_person()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w);
        var guest = (await owner.Post($"{w}/guests", new { firstName = "Ana", lastName = "Lima" })).Json.Id();

        var nobody = await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1 });
        var both = await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1, guestId = guest, personName = "Tia" });
        var byGuest = await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1, guestId = guest });

        Assert.Equal(HttpStatusCode.BadRequest, nobody.Status);
        Assert.Equal(HttpStatusCode.BadRequest, both.Status);
        Assert.Equal("Ana Lima", byGuest.Json.Str("guestName"));
    }

    [Fact]
    public async Task Gift_with_reservations_or_receipts_cannot_be_deleted_only_cancelled()
    {
        var (owner, w, _) = await NewOwner();
        var (_, reserved) = await NewGift(owner, w, 3, name: "A");
        var (_, received) = await NewGift(owner, w, 3, name: "B");
        var (_, free) = await NewGift(owner, w, 3, name: "C");
        var reservation = (await owner.Post($"{w}/gifts/{reserved}/reservations", new { quantity = 1, personName = "Tia" })).Json.Id();
        await owner.Post($"{w}/gift-reservations/{reservation}/cancel");
        await owner.Post($"{w}/gifts/{received}/receipts", new { quantity = 1, personName = "Tia" });

        var withCancelledReservation = await owner.Delete($"{w}/gifts/{reserved}");
        var withReceipt = await owner.Delete($"{w}/gifts/{received}");
        var withoutLinks = await owner.Delete($"{w}/gifts/{free}");
        var cancelled = await owner.Post($"{w}/gifts/{received}/cancel");

        Assert.Equal(HttpStatusCode.Conflict, withCancelledReservation.Status);
        Assert.Equal(HttpStatusCode.Conflict, withReceipt.Status);
        Assert.Equal(HttpStatusCode.NoContent, withoutLinks.Status);
        Assert.Equal("cancelled", cancelled.Json.Str("gift.status"));
    }

    [Fact]
    public async Task Cancelled_gift_blocks_movement_until_reactivated()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w);
        await owner.Post($"{w}/gifts/{gift}/cancel");

        var reservation = await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1, personName = "Tia" });
        var receipt = await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, personName = "Tia" });
        var reactivated = await owner.Post($"{w}/gifts/{gift}/reactivate");
        var afterReactivation = await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1, personName = "Tia" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, reservation.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, receipt.Status);
        Assert.Equal("available", reactivated.Json.Str("gift.status"));
        Assert.Equal(HttpStatusCode.Created, afterReactivation.Status);
    }

    [Fact]
    public async Task Category_with_gifts_cannot_be_deleted()
    {
        var (owner, w, _) = await NewOwner();
        var (category, gift) = await NewGift(owner, w);

        var blocked = await owner.Delete($"{w}/gift-categories/{category}");
        await owner.Delete($"{w}/gifts/{gift}");
        var allowed = await owner.Delete($"{w}/gift-categories/{category}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
        Assert.Equal(HttpStatusCode.NoContent, allowed.Status);
    }

    [Fact]
    public async Task Desired_quantity_cannot_drop_below_what_is_committed()
    {
        var (owner, w, _) = await NewOwner();
        var (category, gift) = await NewGift(owner, w, 5);
        await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 2, personName = "Tia" });
        await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, personName = "Tio" });

        var tooLow = await owner.Put($"{w}/gifts/{gift}", new { name = "Batedeira", categoryId = category, desiredQuantity = 2 });
        var exact = await owner.Put($"{w}/gifts/{gift}", new { name = "Batedeira", categoryId = category, desiredQuantity = 3 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooLow.Status);
        Assert.Equal(HttpStatusCode.OK, exact.Status);
    }

    [Fact]
    public async Task Display_status_goes_through_the_documented_order()
    {
        var (owner, w, _) = await NewOwner();
        var (_, gift) = await NewGift(owner, w, 2);
        var url = $"{w}/gifts/{gift}";

        var available = await owner.Get(url);
        var reservation = (await owner.Post($"{url}/reservations", new { quantity = 2, personName = "Tia" })).Json.Id();
        var reserved = await owner.Get(url);
        await owner.Post($"{url}/receipts", new { quantity = 2, reservationId = reservation });
        var received = await owner.Get(url);

        Assert.Equal("available", available.Json.Str("gift.status"));
        Assert.Equal("reserved", reserved.Json.Str("gift.status"));
        Assert.Equal("received", received.Json.Str("gift.status"));
    }

    [Fact]
    public async Task Search_filters_and_totals()
    {
        var (owner, w, _) = await NewOwner();
        var kitchen = (await owner.Post($"{w}/gift-categories", new { name = "Cozinha" })).Json.Id();
        var home = (await owner.Post($"{w}/gift-categories", new { name = "Casa" })).Json.Id();
        var mixer = (await owner.Post($"{w}/gifts", new { name = "Batedeira", categoryId = kitchen, desiredQuantity = 1, estimatedValue = 500 })).Json.Id("gift.id");
        var plates = (await owner.Post($"{w}/gifts", new { name = "Pratos", description = "Porcelana branca", categoryId = kitchen, desiredQuantity = 4, estimatedValue = 50 })).Json.Id("gift.id");
        await owner.Post($"{w}/gifts", new { name = "Toalhas", categoryId = home, desiredQuantity = 2, estimatedValue = 100 });
        var cancelled = (await owner.Post($"{w}/gifts", new { name = "Vaso", categoryId = home, desiredQuantity = 1, estimatedValue = 900 })).Json.Id("gift.id");
        await owner.Post($"{w}/gifts/{cancelled}/cancel");
        await owner.Post($"{w}/gifts/{mixer}/reservations", new { quantity = 1, personName = "Tia" });
        await owner.Post($"{w}/gifts/{plates}/reservations", new { quantity = 1, personName = "Tia" });
        await owner.Post($"{w}/gifts/{plates}/receipts", new { quantity = 2, personName = "Tio" });

        var byName = await owner.Get($"{w}/gifts?search=batedeira");
        var byDescription = await owner.Get($"{w}/gifts?search=porcelana");
        var byCategoryName = await owner.Get($"{w}/gifts?search=casa");
        var byCategory = await owner.Get($"{w}/gifts?categoryId={kitchen}");
        var byStatus = await owner.Get($"{w}/gifts?status=reserved");
        var byRange = await owner.Get($"{w}/gifts?minValue=60&maxValue=200");
        var available = await owner.Get($"{w}/gifts?available=true");
        var withReservations = await owner.Get($"{w}/gifts?hasReservations=true");
        var withReceipts = await owner.Get($"{w}/gifts?hasReceipts=true");
        var summary = await owner.Get($"{w}/gifts/summary");

        Assert.Equal(1, byName.Json.At("items").Count());
        Assert.Equal("Pratos", byDescription.Json.Str("items.0.name"));
        Assert.Equal(2, byCategoryName.Json.At("items").Count());
        Assert.Equal(2, byCategory.Json.At("items").Count());
        Assert.Equal("Batedeira", byStatus.Json.Str("items.0.name"));
        Assert.Equal("Toalhas", byRange.Json.Str("items.0.name"));
        Assert.Equal(2, available.Json.At("items").Count());
        Assert.Equal(2, withReservations.Json.At("items").Count());
        Assert.Equal("Pratos", withReceipts.Json.Str("items.0.name"));
        Assert.Equal(3, summary.Json.Int("activeGifts"));
        Assert.Equal(7, summary.Json.Int("desiredUnits"));
        Assert.Equal(2, summary.Json.Int("reservedUnits"));
        Assert.Equal(2, summary.Json.Int("receivedUnits"));
        Assert.Equal(3, summary.Json.Int("availableUnits"));
        Assert.Equal(900, summary.Json.Dec("estimatedValue"));
    }
}
