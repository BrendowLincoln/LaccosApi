using Lacoos.Modules.Suppliers.Domain;
using Lacoos.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lacoos.Modules.Suppliers.Persistence;

internal sealed class SuppliersDbContext(DbContextOptions<SuppliersDbContext> options, IWeddingContext wedding)
    : ModuleDbContext(options, wedding, SchemaName)
{
    public const string SchemaName = "suppliers";

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<SupplierService> Services => Set<SupplierService>();

    public DbSet<SupplierContract> Contracts => Set<SupplierContract>();
}

internal sealed class SuppliersDbContextFactory() : ModuleDesignTimeFactory<SuppliersDbContext>(SuppliersDbContext.SchemaName);

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.Ignore(s => s.IsNegotiating);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.ContactName).HasMaxLength(200);
        builder.Property(s => s.Email).HasMaxLength(320);
        builder.Property(s => s.Phone).HasMaxLength(40);
        builder.HasMany(s => s.Services).WithOne().HasForeignKey(s => s.SupplierId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(s => s.Contracts).WithOne().HasForeignKey(c => c.SupplierId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class SupplierServiceConfiguration : IEntityTypeConfiguration<SupplierService>
{
    public void Configure(EntityTypeBuilder<SupplierService> builder)
    {
        builder.ToTable("supplier_services");
        builder.Property(s => s.Name).HasMaxLength(200);
    }
}

internal sealed class SupplierContractConfiguration : IEntityTypeConfiguration<SupplierContract>
{
    public void Configure(EntityTypeBuilder<SupplierContract> builder)
    {
        builder.ToTable("supplier_contracts");
        builder.Property(c => c.ContractNumber).HasMaxLength(100);
        builder.Property(c => c.FileName).HasMaxLength(200);
    }
}
