using System.Net;
using Lacoos.IntegrationTests.Support;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class GuestsTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task First_and_last_name_are_required_and_companions_cannot_be_negative()
    {
        var (owner, w, _) = await NewOwner();

        var missing = await owner.Post($"{w}/guests", new { firstName = "Ana" });
        var negative = await owner.Post($"{w}/guests", new { firstName = "Ana", lastName = "Lima", maxCompanions = -1 });

        Assert.Equal("Sobrenome é obrigatório.", missing.Json.Str("errors.lastName.0"));
        Assert.Equal(HttpStatusCode.BadRequest, negative.Status);
    }

    [Fact]
    public async Task Group_with_guests_cannot_be_deleted()
    {
        var (owner, w, _) = await NewOwner();
        var group = (await owner.Post($"{w}/guest-groups", new { name = "Família", category = "family" })).Json.Id();
        var guest = (await owner.Post($"{w}/guests", new { firstName = "Ana", lastName = "Lima", groupId = group })).Json.Id();

        var blocked = await owner.Delete($"{w}/guest-groups/{group}");
        await owner.Delete($"{w}/guests/{guest}");
        var allowed = await owner.Delete($"{w}/guest-groups/{group}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.Status);
        Assert.Equal(HttpStatusCode.NoContent, allowed.Status);
    }

    [Fact]
    public async Task Marking_as_sent_never_downgrades_an_opened_invite()
    {
        var (owner, w, _) = await NewOwner();
        var fresh = (await owner.Post($"{w}/guests", new { firstName = "Ana", lastName = "Lima" })).Json;
        var opened = (await owner.Post($"{w}/guests", new { firstName = "Bia", lastName = "Reis", invitationStatus = "opened" })).Json;

        var sent = await owner.Post($"{w}/guests/{fresh.Id()}/mark-sent");
        var stillOpened = await owner.Post($"{w}/guests/{opened.Id()}/mark-sent");

        Assert.Equal("not_sent", fresh.Str("invitationStatus"));
        Assert.Equal("sent", sent.Json.Str("invitationStatus"));
        Assert.Equal("opened", stillOpened.Json.Str("invitationStatus"));
    }

    [Fact]
    public async Task Search_filters_and_summary()
    {
        var (owner, w, _) = await NewOwner();
        var family = (await owner.Post($"{w}/guest-groups", new { name = "Família da noiva", category = "family" })).Json.Id();
        var work = (await owner.Post($"{w}/guest-groups", new { name = "Trabalho", category = "work" })).Json.Id();
        await owner.Post($"{w}/guests", new { firstName = "Ana", lastName = "Lima", groupId = family, email = "ana@example.com", maxCompanions = 2, attendanceStatus = "confirmed", invitationStatus = "opened" });
        await owner.Post($"{w}/guests", new { firstName = "Bruno", lastName = "Reis", groupId = work, phone = "31988887777", maxCompanions = 1, attendanceStatus = "declined", invitationStatus = "sent" });
        await owner.Post($"{w}/guests", new { firstName = "Carla", lastName = "Dias" });
        await owner.Post($"{w}/guests", new { firstName = "Davi", lastName = "Melo", attendanceStatus = "confirmed" });

        var byName = await owner.Get($"{w}/guests?search=ana lima");
        var byEmail = await owner.Get($"{w}/guests?search=ana@example");
        var byPhone = await owner.Get($"{w}/guests?search=8888");
        var byGroupName = await owner.Get($"{w}/guests?search=noiva");
        var byGroup = await owner.Get($"{w}/guests?groupId={work}");
        var byCategory = await owner.Get($"{w}/guests?groupCategory=family");
        var byInvite = await owner.Get($"{w}/guests?invitationStatus=not_sent");
        var byAttendance = await owner.Get($"{w}/guests?attendanceStatus=confirmed");
        var summary = await owner.Get($"{w}/guests/summary");

        Assert.Equal(1, byName.Json.At("items").Count());
        Assert.Equal(1, byEmail.Json.At("items").Count());
        Assert.Equal("Bruno", byPhone.Json.Str("items.0.firstName"));
        Assert.Equal("Ana", byGroupName.Json.Str("items.0.firstName"));
        Assert.Equal("Bruno", byGroup.Json.Str("items.0.firstName"));
        Assert.Equal("Ana", byCategory.Json.Str("items.0.firstName"));
        Assert.Equal(2, byInvite.Json.At("items").Count());
        Assert.Equal(2, byAttendance.Json.At("items").Count());
        Assert.Equal(4, summary.Json.Int("total"));
        Assert.Equal(2, summary.Json.Int("confirmed"));
        Assert.Equal(1, summary.Json.Int("pending"));
        Assert.Equal(1, summary.Json.Int("declined"));
        Assert.Equal(50, summary.Json.Dec("confirmedPercent"));
        Assert.Equal(3, summary.Json.Int("companionsAllowed"));
        Assert.Equal(2, summary.Json.Int("invitationsSent"));
        Assert.Equal(1, summary.Json.Int("invitationsOpened"));
    }

    [Fact]
    public async Task Deleting_a_guest_keeps_their_gift_reservations_and_receipts()
    {
        var (owner, w, _) = await NewOwner();
        var guest = (await owner.Post($"{w}/guests", new { firstName = "Ana", lastName = "Lima" })).Json.Id();
        var category = (await owner.Post($"{w}/gift-categories", new { name = "Casa" })).Json.Id();
        var gift = (await owner.Post($"{w}/gifts", new { name = "Vaso", categoryId = category, desiredQuantity = 3 })).Json.Id("gift.id");
        await owner.Post($"{w}/gifts/{gift}/reservations", new { quantity = 1, guestId = guest });
        await owner.Post($"{w}/gifts/{gift}/receipts", new { quantity = 1, guestId = guest });

        var deleted = await owner.Delete($"{w}/guests/{guest}");
        var detail = await owner.Get($"{w}/gifts/{gift}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Equal("Ana Lima", detail.Json.Str("reservations.0.personName"));
        Assert.Equal("Ana Lima", detail.Json.Str("receipts.0.personName"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, detail.Json.At("reservations.0.guestId").ValueKind);
        Assert.Equal(1, detail.Json.Int("gift.reservedUnits"));
        Assert.Equal(1, detail.Json.Int("gift.receivedUnits"));
    }
}
