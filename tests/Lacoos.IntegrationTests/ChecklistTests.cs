using System.Net;
using Lacoos.IntegrationTests.Support;
using Lacoos.SharedKernel.Tenancy;
using Xunit;

namespace Lacoos.IntegrationTests;

public sealed class ChecklistTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Title_is_required()
    {
        var (owner, w, _) = await NewOwner();

        var response = await owner.Post($"{w}/checklist/items", new { title = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);
        Assert.Equal("Título é obrigatório.", response.Json.Str("errors.title.0"));
    }

    [Fact]
    public async Task Crud_keeps_contract_enums_and_dates()
    {
        var (owner, w, _) = await NewOwner();

        var created = await owner.Post($"{w}/checklist/items", new { title = "Tarefa", dueDate = "2030-02-03", priority = "high", status = "in_progress" });
        var id = created.Json.Id();
        var updated = await owner.Put($"{w}/checklist/items/{id}", new { title = "Editada", status = "done" });
        var deleted = await owner.Delete($"{w}/checklist/items/{id}");
        var gone = await owner.Get($"{w}/checklist/items/{id}");

        Assert.Equal("in_progress", created.Json.Str("status"));
        Assert.Equal("high", created.Json.Str("priority"));
        Assert.Equal("2030-02-03", created.Json.Str("dueDate"));
        Assert.Equal("done", updated.Json.Str("status"));
        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Equal(HttpStatusCode.NotFound, gone.Status);
    }

    [Fact]
    public async Task Pending_member_can_be_assigned()
    {
        var (owner, w, _) = await NewOwner();
        var invite = await owner.Post($"{w}/members", new { name = "Pendente", email = $"{Guid.NewGuid():N}@example.com", role = "organizer" });

        var task = await owner.Post($"{w}/checklist/items", new { title = "T", assignedToMemberId = invite.Json.Id("member.id") });

        Assert.Equal(HttpStatusCode.Created, task.Status);
        Assert.Equal("Pendente", task.Json.Str("assignedToMemberName"));
    }

    [Fact]
    public async Task Inactive_category_cannot_be_chosen_but_stays_on_existing_tasks()
    {
        var (owner, w, _) = await NewOwner();
        var category = (await owner.Post($"{w}/checklist/categories", new { name = "Antiga" })).Json.Id();
        var task = (await owner.Post($"{w}/checklist/items", new { title = "T", categoryId = category })).Json.Id();
        var other = (await owner.Post($"{w}/checklist/items", new { title = "Sem categoria" })).Json.Id();

        await owner.Patch($"{w}/checklist/categories/{category}/active", new { active = false });

        var newTask = await owner.Post($"{w}/checklist/items", new { title = "N", categoryId = category });
        var moveOther = await owner.Put($"{w}/checklist/items/{other}", new { title = "Sem categoria", categoryId = category });
        var keep = await owner.Put($"{w}/checklist/items/{task}", new { title = "Editada", categoryId = category });
        var activeOnly = await owner.Get($"{w}/checklist/categories?active=true");
        var all = await owner.Get($"{w}/checklist/categories");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, newTask.Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, moveOther.Status);
        Assert.Equal(HttpStatusCode.OK, keep.Status);
        Assert.Equal("Antiga", keep.Json.Str("categoryName"));
        Assert.Equal(0, activeOnly.Json.Count());
        Assert.Equal(1, all.Json.Count());
    }

    [Fact]
    public async Task List_filters_searches_sorts_and_totals()
    {
        var (owner, w, weddingId) = await NewOwner();
        var (_, memberId) = await Api.AddMember(owner, weddingId, WeddingRole.Organizer);
        var category = (await owner.Post($"{w}/checklist/categories", new { name = "Festa" })).Json.Id();

        await owner.Post($"{w}/checklist/items", new { title = "Reservar buffet", dueDate = "2030-03-01", priority = "high", status = "done", categoryId = category });
        await owner.Post($"{w}/checklist/items", new { title = "Comprar alianças", dueDate = "2030-01-01", priority = "low", status = "in_progress", assignedToMemberId = memberId });
        await owner.Post($"{w}/checklist/items", new { title = "Escolher música", description = "Banda para o BUFFET", dueDate = "2030-02-01" });
        await owner.Post($"{w}/checklist/items", new { title = "100% pronto" });

        var byDue = await owner.Get($"{w}/checklist/items");
        var byDueDesc = await owner.Get($"{w}/checklist/items?sort=-dueDate");
        var search = await owner.Get($"{w}/checklist/items?search=buffet");
        var wildcard = await owner.Get($"{w}/checklist/items?search=%25");
        var status = await owner.Get($"{w}/checklist/items?status=in_progress");
        var priority = await owner.Get($"{w}/checklist/items?priority=high");
        var byCategory = await owner.Get($"{w}/checklist/items?categoryId={category}");
        var byMember = await owner.Get($"{w}/checklist/items?assignedToMemberId={memberId}");
        var period = await owner.Get($"{w}/checklist/items?dueFrom=2030-01-15&dueTo=2030-02-15");
        var invalid = await owner.Get($"{w}/checklist/items?status=feito");

        Assert.Equal("Comprar alianças", byDue.Json.Str("items.0.title"));
        Assert.Equal("100% pronto", byDue.Json.Str("items.3.title"));
        Assert.Equal("Reservar buffet", byDueDesc.Json.Str("items.0.title"));
        Assert.Equal(2, search.Json.At("items").Count());
        Assert.Equal(1, wildcard.Json.At("items").Count());
        Assert.Equal(1, status.Json.At("items").Count());
        Assert.Equal(1, priority.Json.At("items").Count());
        Assert.Equal(1, byCategory.Json.At("items").Count());
        Assert.Equal(1, byMember.Json.At("items").Count());
        Assert.Equal("Escolher música", period.Json.Str("items.0.title"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.Status);
        Assert.Equal(4, status.Json.Int("totals.total"));
        Assert.Equal(2, status.Json.Int("totals.pending"));
        Assert.Equal(1, status.Json.Int("totals.inProgress"));
        Assert.Equal(1, status.Json.Int("totals.done"));
        Assert.Equal(25, status.Json.Dec("totals.completionPercent"));
    }
}
