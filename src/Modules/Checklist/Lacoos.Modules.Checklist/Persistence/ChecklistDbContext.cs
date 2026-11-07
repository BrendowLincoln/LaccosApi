using Lacoos.Modules.Checklist.Domain;
using Lacoos.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lacoos.Modules.Checklist.Persistence;

internal sealed class ChecklistDbContext(DbContextOptions<ChecklistDbContext> options, IWeddingContext wedding)
    : ModuleDbContext(options, wedding, SchemaName)
{
    public const string SchemaName = "checklist";

    public DbSet<ChecklistCategory> Categories => Set<ChecklistCategory>();

    public DbSet<ChecklistItem> Items => Set<ChecklistItem>();
}

internal sealed class ChecklistDbContextFactory() : ModuleDesignTimeFactory<ChecklistDbContext>(ChecklistDbContext.SchemaName);

internal sealed class ChecklistCategoryConfiguration : IEntityTypeConfiguration<ChecklistCategory>
{
    public void Configure(EntityTypeBuilder<ChecklistCategory> builder)
    {
        builder.ToTable("checklist_categories");
        builder.Property(c => c.Name).HasMaxLength(200);
    }
}

internal sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("checklist_items");
        builder.Property(i => i.Title).HasMaxLength(300);
        builder.HasIndex(i => i.AssignedToMemberId);
        builder.HasOne(i => i.Category).WithMany().HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
