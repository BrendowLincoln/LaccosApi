using Lacoos.Modules.Budget.Domain;
using Lacoos.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lacoos.Modules.Budget.Persistence;

internal sealed class BudgetDbContext(DbContextOptions<BudgetDbContext> options, IWeddingContext wedding)
    : ModuleDbContext(options, wedding, SchemaName)
{
    public const string SchemaName = "budget";

    public DbSet<BudgetSettings> Settings => Set<BudgetSettings>();

    public DbSet<BudgetCategory> Categories => Set<BudgetCategory>();

    public DbSet<BudgetItem> Items => Set<BudgetItem>();
}

internal sealed class BudgetDbContextFactory() : ModuleDesignTimeFactory<BudgetDbContext>(BudgetDbContext.SchemaName);

internal sealed class BudgetSettingsConfiguration : IEntityTypeConfiguration<BudgetSettings>
{
    public void Configure(EntityTypeBuilder<BudgetSettings> builder)
    {
        builder.ToTable("budget_settings");
        builder.HasKey(s => s.WeddingId);
    }
}

internal sealed class BudgetCategoryConfiguration : IEntityTypeConfiguration<BudgetCategory>
{
    public void Configure(EntityTypeBuilder<BudgetCategory> builder)
    {
        builder.ToTable("budget_categories");
        builder.Property(c => c.Name).HasMaxLength(200);
    }
}

internal sealed class BudgetItemConfiguration : IEntityTypeConfiguration<BudgetItem>
{
    public void Configure(EntityTypeBuilder<BudgetItem> builder)
    {
        builder.ToTable("budget_items");
        builder.Ignore(i => i.CommittedAmount);
        builder.Property(i => i.Name).HasMaxLength(200);
        builder.HasIndex(i => i.SupplierId);
        builder.HasOne(i => i.Category).WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
