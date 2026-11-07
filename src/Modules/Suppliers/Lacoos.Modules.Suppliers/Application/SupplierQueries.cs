using Lacoos.Modules.Suppliers.Domain;
using Lacoos.Modules.Suppliers.Persistence;
using Lacoos.SharedKernel.Modules;

namespace Lacoos.Modules.Suppliers.Application;

internal sealed class SupplierQueries(SuppliersDbContext db) : ISupplierQueries
{
    public Task<bool> ExistsAsync(Guid supplierId, CancellationToken ct) =>
        db.Suppliers.AnyAsync(s => s.Id == supplierId, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> supplierIds, CancellationToken ct) =>
        await db.Suppliers.AsNoTracking()
            .Where(s => supplierIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

    public async Task<IReadOnlyList<Guid>> SearchIdsAsync(string search, CancellationToken ct)
    {
        var pattern = Text.ContainsPattern(search);
        return await db.Suppliers.AsNoTracking()
            .Where(s => EF.Functions.ILike(s.Name, pattern, @"\"))
            .Select(s => s.Id)
            .ToListAsync(ct);
    }
}

internal sealed class SuppliersSeeder(SuppliersDbContext db, WeddingContext weddingContext) : IModuleSeeder
{
    public int Order => 50;

    public async Task SeedAsync(DemoSeedOptions options, CancellationToken ct)
    {
        weddingContext.Set(DemoIds.Wedding, null);
        if (await db.Suppliers.AnyAsync(ct))
        {
            return;
        }

        db.Suppliers.AddRange(
            new Supplier
            {
                Id = DemoIds.PhotoSupplier,
                Name = "Luz & Foco Fotografia",
                Category = "Fotografia",
                ContactName = "Marina Luz",
                Email = "contato@luzefoco.example.com",
                NegotiationStatus = NegotiationStatus.Hired,
                Services =
                [
                    new SupplierService { WeddingId = DemoIds.Wedding, Name = "Cobertura completa", QuotedAmount = 7500, ContractedAmount = 6800, Status = ServiceStatus.Contracted },
                    new SupplierService { WeddingId = DemoIds.Wedding, Name = "Álbum extra", QuotedAmount = 1200, Status = ServiceStatus.Cancelled },
                ],
                Contracts = [new SupplierContract { WeddingId = DemoIds.Wedding, ContractNumber = "2026-014", Status = ContractStatus.Signed }],
            },
            new Supplier
            {
                Id = DemoIds.VenueSupplier,
                Name = "Sítio das Palmeiras",
                Category = "Local",
                ContactName = "Roberto",
                Phone = "3133330000",
                NegotiationStatus = NegotiationStatus.Hired,
                Services = [new SupplierService { WeddingId = DemoIds.Wedding, Name = "Locação do espaço", QuotedAmount = 18000, ContractedAmount = 16500, Status = ServiceStatus.Contracted }],
            },
            new Supplier
            {
                Id = DemoIds.BuffetSupplier,
                Name = "Sabor da Serra Buffet",
                Category = "Buffet",
                Email = "eventos@sabordaserra.example.com",
                NegotiationStatus = NegotiationStatus.Negotiating,
                Services = [new SupplierService { WeddingId = DemoIds.Wedding, Name = "Jantar para 120 pessoas", QuotedAmount = 24000 }],
            },
            new Supplier { Name = "Flor de Maio", Category = "Decoração", NegotiationStatus = NegotiationStatus.Quoted },
            new Supplier { Name = "Banda Aurora", Category = "Música", NegotiationStatus = NegotiationStatus.Researching });
        await db.SaveChangesAsync(ct);
    }
}
