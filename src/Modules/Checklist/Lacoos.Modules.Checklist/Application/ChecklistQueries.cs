using Lacoos.Modules.Checklist.Domain;
using Lacoos.Modules.Checklist.Persistence;
using Lacoos.SharedKernel.Modules;

namespace Lacoos.Modules.Checklist.Application;

internal sealed class ChecklistQueries(ChecklistDbContext db) : IChecklistQueries, IChecklistAssignments
{
    public async Task<ChecklistSummary> GetSummaryAsync(CancellationToken ct)
    {
        var total = await db.Items.CountAsync(ct);
        var done = await db.Items.CountAsync(i => i.Status == ChecklistStatus.Done, ct);
        return new ChecklistSummary(total, done, total - done, Text.Percent(done, total));
    }

    public async Task<IReadOnlyList<UpcomingTask>> GetUpcomingTasksAsync(DateOnly until, int limit, CancellationToken ct)
    {
        var items = await db.Items.AsNoTracking()
            .Where(i => i.Status != ChecklistStatus.Done && i.DueDate != null && i.DueDate <= until)
            .OrderBy(i => i.DueDate).ThenBy(i => i.Title)
            .Take(limit)
            .ToListAsync(ct);
        return items.Select(i => new UpcomingTask(
            i.Id, i.Title, i.DueDate!.Value, EnumText.ToText(i.Priority), EnumText.ToText(i.Status), i.AssignedToMemberId)).ToList();
    }

    public Task UnassignMemberAsync(Guid memberId, CancellationToken ct) =>
        db.Items.Where(i => i.AssignedToMemberId == memberId)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.AssignedToMemberId, (Guid?)null), ct);
}

internal sealed class ChecklistSeeder(ChecklistDbContext db, WeddingContext weddingContext, IClock clock) : IModuleSeeder
{
    public int Order => 30;

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken ct)
    {
        weddingContext.Set(DemoIds.Wedding, null);
        if (await db.Categories.AnyAsync(ct))
        {
            return;
        }

        var planning = new ChecklistCategory { Name = "Planejamento" };
        var ceremony = new ChecklistCategory { Name = "Documentação", Description = "Cartório e papéis" };
        var legacy = new ChecklistCategory { Name = "Ensaio pré-wedding", Active = false };
        db.Categories.AddRange(planning, ceremony, legacy);

        var today = clock.Today;
        db.Items.AddRange(
            new ChecklistItem { Title = "Definir lista de convidados", Category = planning, Status = ChecklistStatus.Done, Priority = ChecklistPriority.High, DueDate = today.AddDays(-20), AssignedToMemberId = DemoIds.OwnerMember },
            new ChecklistItem { Title = "Visitar o local da festa", Category = planning, Status = ChecklistStatus.InProgress, Priority = ChecklistPriority.High, DueDate = today.AddDays(5), AssignedToMemberId = DemoIds.OrganizerMember },
            new ChecklistItem { Title = "Dar entrada no cartório", Category = ceremony, Priority = ChecklistPriority.High, DueDate = today.AddDays(-2), AssignedToMemberId = DemoIds.OwnerMember },
            new ChecklistItem { Title = "Fechar degustação do buffet", Category = planning, Priority = ChecklistPriority.Medium, DueDate = today.AddDays(18) },
            new ChecklistItem { Title = "Escolher lembrancinhas", Category = planning, Priority = ChecklistPriority.Low, DueDate = today.AddDays(90) },
            new ChecklistItem { Title = "Fazer ensaio fotográfico", Category = legacy, Priority = ChecklistPriority.Low });
        await db.SaveChangesAsync(ct);
    }
}
