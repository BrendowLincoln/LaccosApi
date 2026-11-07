using System.Net;
using Lacoos.IntegrationTests.Support;
using Lacoos.SharedKernel.Tenancy;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class WeddingsTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Creator_becomes_owner_and_status_starts_as_planning()
    {
        var user = await Api.NewUser("Criadora");

        var created = await user.Post("/api/weddings", new { name = "Festa", weddingDate = "2030-05-20", estimatedGuestCount = 80 });
        var members = await user.Get($"{W(created.Json.Id())}/members");

        Assert.Equal("planning", created.Json.Str("status"));
        Assert.Equal("2030-05-20", created.Json.Str("weddingDate"));
        Assert.Equal("owner", members.Json.Str("0.role"));
        Assert.Equal("active", members.Json.Str("0.status"));
        Assert.Equal("Criadora", members.Json.Str("0.name"));
    }

    [Fact]
    public async Task User_can_participate_in_several_weddings()
    {
        var user = await Api.NewUser();
        await user.NewWedding();
        await user.NewWedding();

        var list = await user.Get("/api/weddings");

        Assert.Equal(2, list.Json.Count());
        Assert.Equal("owner", list.Json.Str("0.role"));
    }

    [Fact]
    public async Task Wedding_time_uses_the_contract_format()
    {
        var (owner, w, _) = await NewOwner();

        var updated = await owner.Put(w, new { name = "Festa", weddingDate = "2030-05-20", estimatedGuestCount = 80, time = "16:30", city = "BH" });

        Assert.Equal(HttpStatusCode.OK, updated.Status);
        Assert.Equal("16:30", updated.Json.Str("time"));
    }

    [Fact]
    public async Task Invalid_body_format_gets_a_portuguese_error()
    {
        var (owner, w, _) = await NewOwner();

        var response = await owner.Put(w, new { name = "Festa", weddingDate = "20/05/2030", estimatedGuestCount = 80 });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Contains("inválido", response.Json.Str("detail"));
    }

    [Fact]
    public async Task Status_can_be_changed_to_a_contract_value()
    {
        var (owner, w, _) = await NewOwner();

        var changed = await owner.Patch($"{w}/status", new { status = "confirmed" });
        var invalid = await owner.Patch($"{w}/status", new { status = "finalizado" });

        Assert.Equal("confirmed", changed.Json.Str("status"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
    }

    [Fact]
    public async Task Invite_sends_a_real_email_and_accepting_activates_the_member()
    {
        var (owner, w, weddingId) = await NewOwner();
        var invited = await Api.NewUser("Convidada");

        var invite = await owner.Post($"{w}/members", new { name = "Nome do convite", email = invited.Email, role = "organizer" });
        var token = Api.Emails.LastTokenFor(invited.Email);
        var beforeAccept = await invited.Get(w);
        var preview = await Api.Anonymous().Get($"/api/invitations/{token}");
        var accept = await invited.Post($"/api/invitations/{token}/accept");
        var afterAccept = await invited.Get(w);
        var members = await owner.Get($"{w}/members");

        Assert.True(invite.Json.At("inviteEmailSent").GetBoolean());
        Assert.Equal("invited", invite.Json.Str("member.status"));
        Assert.Equal(HttpStatusCode.NotFound, beforeAccept.Status);
        Assert.Equal(invited.Email, preview.Json.Str("email"));
        Assert.Equal(weddingId, accept.Json.Id("weddingId"));
        Assert.Equal(HttpStatusCode.OK, afterAccept.Status);
        Assert.Equal("organizer", afterAccept.Json.Str("role"));
        var member = members.Json.EnumerateArray().Single(m => m.Str("email") == invited.Email);
        Assert.Equal("active", member.Str("status"));
        Assert.Equal("Convidada", member.Str("name"));
    }

    [Fact]
    public async Task Invite_survives_email_failure_and_reports_it()
    {
        var (owner, w, _) = await NewOwner();
        Api.Emails.ShouldFail = true;

        var invite = await owner.Post($"{w}/members", new { name = "X", email = $"{Guid.NewGuid():N}@example.com", role = "viewer" });

        Assert.Equal(HttpStatusCode.Created, invite.Status);
        Assert.False(invite.Json.At("inviteEmailSent").GetBoolean());
    }

    [Fact]
    public async Task Invitation_for_another_email_cannot_be_accepted()
    {
        var (owner, w, _) = await NewOwner();
        var email = $"{Guid.NewGuid():N}@example.com";
        await owner.Post($"{w}/members", new { name = "X", email, role = "viewer" });
        var stranger = await Api.NewUser();

        var accept = await stranger.Post($"/api/invitations/{Api.Emails.LastTokenFor(email)}/accept");

        Assert.Equal(HttpStatusCode.Forbidden, accept.Status);
    }

    [Fact]
    public async Task Invitation_cannot_be_used_twice()
    {
        var (owner, w, _) = await NewOwner();
        var invited = await Api.NewUser();
        await owner.Post($"{w}/members", new { name = "X", email = invited.Email, role = "viewer" });
        var token = Api.Emails.LastTokenFor(invited.Email);

        await invited.Post($"/api/invitations/{token}/accept");
        var second = await invited.Post($"/api/invitations/{token}/accept");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.Status);
    }

    [Fact]
    public async Task Invitation_expires()
    {
        var (owner, w, _) = await NewOwner();
        var invited = await Api.NewUser();
        await owner.Post($"{w}/members", new { name = "X", email = invited.Email, role = "viewer" });
        var token = Api.Emails.LastTokenFor(invited.Email);

        var expired = Api.Anonymous();
        Api.Clock.FixedAt = DateTimeOffset.UtcNow.AddDays(8);
        var preview = await expired.Get($"/api/invitations/{token}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, preview.Status);
    }

    [Fact]
    public async Task Resend_replaces_the_token_and_only_works_for_pending_invites()
    {
        var (owner, w, _) = await NewOwner();
        var invited = await Api.NewUser();
        var invite = await owner.Post($"{w}/members", new { name = "X", email = invited.Email, role = "viewer" });
        var memberId = invite.Json.Id("member.id");
        var oldToken = Api.Emails.LastTokenFor(invited.Email);

        var resend = await owner.Post($"{w}/members/{memberId}/resend-invite");
        var newToken = Api.Emails.LastTokenFor(invited.Email);
        var withOldToken = await invited.Post($"/api/invitations/{oldToken}/accept");
        var withNewToken = await invited.Post($"/api/invitations/{newToken}/accept");
        var resendActive = await owner.Post($"{w}/members/{memberId}/resend-invite");

        Assert.Equal(HttpStatusCode.OK, resend.Status);
        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, withOldToken.Status);
        Assert.Equal(HttpStatusCode.OK, withNewToken.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, resendActive.Status);
    }

    [Fact]
    public async Task Same_email_cannot_be_invited_twice()
    {
        var (owner, w, _) = await NewOwner();
        var email = $"{Guid.NewGuid():N}@example.com";
        await owner.Post($"{w}/members", new { name = "X", email, role = "viewer" });

        var second = await owner.Post($"{w}/members", new { name = "Y", email = email.ToUpperInvariant(), role = "viewer" });
        var ownerAgain = await owner.Post($"{w}/members", new { name = "Z", email = owner.Email, role = "viewer" });

        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal(HttpStatusCode.Conflict, ownerAgain.Status);
    }

    [Fact]
    public async Task Last_owner_cannot_be_removed_or_demoted()
    {
        var (owner, w, _) = await NewOwner();
        var members = await owner.Get($"{w}/members");
        var memberId = members.Json.Id("0.id");

        var removed = await owner.Delete($"{w}/members/{memberId}");
        var demoted = await owner.Patch($"{w}/members/{memberId}", new { role = "viewer" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, removed.Status);
        Assert.Equal("O casamento não pode ficar sem owner.", removed.Json.Str("detail"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, demoted.Status);
    }

    [Fact]
    public async Task Pending_owner_invite_does_not_count_as_an_owner()
    {
        var (owner, w, _) = await NewOwner();
        var ownerMemberId = (await owner.Get($"{w}/members")).Json.Id("0.id");
        await owner.Post($"{w}/members", new { name = "X", email = $"{Guid.NewGuid():N}@example.com", role = "owner" });

        var removed = await owner.Delete($"{w}/members/{ownerMemberId}");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, removed.Status);
    }

    [Fact]
    public async Task Two_owners_allow_one_to_leave_but_not_both_at_once()
    {
        var (owner, w, weddingId) = await NewOwner();
        var ownerMemberId = (await owner.Get($"{w}/members")).Json.Id("0.id");
        var (second, secondMemberId) = await Api.AddMember(owner, weddingId, WeddingRole.Owner);

        var responses = await Task.WhenAll(
            owner.Delete($"{w}/members/{secondMemberId}"),
            second.Delete($"{w}/members/{ownerMemberId}"));

        var remaining = await (responses[0].Status == HttpStatusCode.NoContent ? owner : second).Get($"{w}/members");
        Assert.Single(responses, r => r.Status == HttpStatusCode.NoContent);
        Assert.Equal(1, remaining.Json.EnumerateArray().Count(m => m.Str("role") == "owner"));
    }

    [Fact]
    public async Task Removing_a_member_leaves_their_tasks_without_assignee()
    {
        var (owner, w, weddingId) = await NewOwner();
        var (_, memberId) = await Api.AddMember(owner, weddingId, WeddingRole.Organizer);
        var task = await owner.Post($"{w}/checklist/items", new { title = "Tarefa", assignedToMemberId = memberId });

        var removed = await owner.Delete($"{w}/members/{memberId}");
        var reloaded = await owner.Get($"{w}/checklist/items/{task.Json.Id()}");

        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, reloaded.Json.At("assignedToMemberId").ValueKind);
    }

    [Fact]
    public async Task Removed_member_loses_access_immediately()
    {
        var (owner, w, weddingId) = await NewOwner();
        var (member, memberId) = await Api.AddMember(owner, weddingId, WeddingRole.Organizer);

        await owner.Delete($"{w}/members/{memberId}");

        Assert.Equal(HttpStatusCode.NotFound, (await member.Get(w)).Status);
    }

    [Fact]
    public async Task Active_member_data_comes_from_the_profile()
    {
        var (owner, w, weddingId) = await NewOwner();
        var (member, memberId) = await Api.AddMember(owner, weddingId, WeddingRole.Viewer);
        await member.Patch("/api/me", new { name = "Nome Atualizado" });

        var rename = await owner.Patch($"{w}/members/{memberId}", new { name = "Outro" });
        var list = await owner.Get($"{w}/members");
        var pending = await owner.Post($"{w}/members", new { name = "Pendente", email = $"{Guid.NewGuid():N}@example.com", role = "viewer" });
        var renamePending = await owner.Patch($"{w}/members/{pending.Json.Id("member.id")}", new { name = "Corrigido" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, rename.Status);
        Assert.Contains(list.Json.EnumerateArray(), m => m.Str("name") == "Nome Atualizado");
        Assert.Equal("Corrigido", renamePending.Json.Str("name"));
    }

    [Fact]
    public async Task Organizer_edits_modules_but_not_the_wedding_or_members()
    {
        var (owner, w, weddingId) = await NewOwner();
        var (organizer, _) = await Api.AddMember(owner, weddingId, WeddingRole.Organizer);

        var task = await organizer.Post($"{w}/checklist/items", new { title = "Pode" });
        var edit = await organizer.Put(w, new { name = "Não", weddingDate = "2030-01-01", estimatedGuestCount = 1 });
        var status = await organizer.Patch($"{w}/status", new { status = "done" });
        var invite = await organizer.Post($"{w}/members", new { name = "X", email = "x@example.com", role = "viewer" });

        Assert.Equal(HttpStatusCode.Created, task.Status);
        Assert.Equal(HttpStatusCode.Forbidden, edit.Status);
        Assert.Equal(HttpStatusCode.Forbidden, status.Status);
        Assert.Equal(HttpStatusCode.Forbidden, invite.Status);
    }

    [Fact]
    public async Task Viewer_only_reads()
    {
        var (owner, w, weddingId) = await NewOwner();
        var (viewer, _) = await Api.AddMember(owner, weddingId, WeddingRole.Viewer);

        var read = await viewer.Get($"{w}/checklist/items");
        var write = await viewer.Post($"{w}/checklist/items", new { title = "Não pode" });

        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal(HttpStatusCode.Forbidden, write.Status);
    }
}
