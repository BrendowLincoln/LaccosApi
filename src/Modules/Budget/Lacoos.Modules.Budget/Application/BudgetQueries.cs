using Lacoos.Modules.Budget.Domain;
using Lacoos.Modules.Budget.Persistence;
using Lacoos.SharedKernel.Modules;

namespace Lacoos.Modules.Budget.Application;

internal sealed class BudgetQueries(BudgetDbContext db) : IBudgetQueries
{
    public Task<bool> ItemExistsAsync(Guid budgetItemId, CancellationToken ct) =>
        db.Items.AnyAsync(i => i.Id == budgetItemId, ct);

    public Task<bool> HasItemsForSupplierAsync(Guid supplierId, CancellationToken ct) =>
        db.Items.AnyAsync(i => i.SupplierId == supplierId, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetItemNamesAsync(IReadOnlyCollection<Guid> itemIds, CancellationToken ct) =>
        await db.Items.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, ct);

    public async Task<IReadOnlyList<Guid>> SearchItemIdsAsync(string search, CancellationToken ct)
    {
        var pattern = Text.ContainsPattern(search);
        return await db.Items.AsNoTracking()
            .Where(i => EF.Functions.ILike(i.Name, pattern, @"\"))
            .Select(i => i.Id)
            .ToListAsync(ct);
    }

    public async Task<BudgetFinancials> GetFinancialsAsync(CancellationToken ct)
    {
        var defined = await db.Settings.AsNoTracking().Select(s => (decimal?)s.TotalAmount).FirstOrDefaultAsync(ct);
        var planned = await db.Categories.SumAsync(c => c.PlannedAmount, ct);
        var items = await db.Items.AsNoTracking().ToListAsync(ct);

        var total = BudgetMath.TotalBudget(defined, planned);
        var committed = items.Sum(i => i.CommittedAmount);
        return new BudgetFinancials(total, committed, total - committed, Text.Percent(committed, total));
    }
}

internal sealed class BudgetSeeder(BudgetDbContext db, WeddingContext weddingContext) : IModuleSeeder
{
    public int Order => 60;

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken ct)
    {
        weddingContext.Set(DemoIds.Wedding, null);
        if (await db.Categories.AnyAsync(ct))
        {
            return;
        }

        db.Settings.Add(new BudgetSettings { WeddingId = DemoIds.Wedding, TotalAmount = 70000 });

        var venue = new BudgetCategory { Name = "Local e buffet", PlannedAmount = 45000 };
        var photo = new BudgetCategory { Name = "Fotografia", PlannedAmount = 8000 };
        var decoration = new BudgetCategory { Name = "Decoração", PlannedAmount = 12000 };
        db.Categories.AddRange(venue, photo, decoration);

        db.Items.AddRange(
            new BudgetItem { Id = DemoIds.VenueBudgetItem, Category = venue, SupplierId = DemoIds.VenueSupplier, Name = "Locação do sítio", PlannedAmount = 18000, ContractedAmount = 16500, Status = BudgetItemStatus.Contracted },
            new BudgetItem { Id = DemoIds.BuffetBudgetItem, Category = venue, SupplierId = DemoIds.BuffetSupplier, Name = "Buffet", PlannedAmount = 26000 },
            new BudgetItem { Id = DemoIds.PhotoBudgetItem, Category = photo, SupplierId = DemoIds.PhotoSupplier, Name = "Fotografia completa", PlannedAmount = 8000, ContractedAmount = 6800, Status = BudgetItemStatus.Contracted },
            new BudgetItem { Category = decoration, Name = "Flores do altar", PlannedAmount = 5000, ContractedAmount = 4800, Status = BudgetItemStatus.Cancelled });
        await db.SaveChangesAsync(ct);
    }
}
