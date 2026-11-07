using Lacoos.Modules.Guests.Domain;
using Lacoos.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lacoos.Modules.Guests.Persistence;

internal sealed class GuestsDbContext(DbContextOptions<GuestsDbContext> options, IWeddingContext wedding)
    : ModuleDbContext(options, wedding, SchemaName)
{
    public const string SchemaName = "guests";

    public DbSet<GuestGroup> Groups => Set<GuestGroup>();

    public DbSet<Guest> Guests => Set<Guest>();
}

internal sealed class GuestsDbContextFactory() : ModuleDesignTimeFactory<GuestsDbContext>(GuestsDbContext.SchemaName);

internal sealed class GuestGroupConfiguration : IEntityTypeConfiguration<GuestGroup>
{
    public void Configure(EntityTypeBuilder<GuestGroup> builder)
    {
        builder.ToTable("guest_groups");
        builder.Property(g => g.Name).HasMaxLength(200);
    }
}

internal sealed class GuestConfiguration : IEntityTypeConfiguration<Guest>
{
    public void Configure(EntityTypeBuilder<Guest> builder)
    {
        builder.Ignore(g => g.FullName);
        builder.Property(g => g.FirstName).HasMaxLength(100);
        builder.Property(g => g.LastName).HasMaxLength(100);
        builder.Property(g => g.Email).HasMaxLength(320);
        builder.Property(g => g.Phone).HasMaxLength(40);
        builder.HasOne(g => g.Group).WithMany().HasForeignKey(g => g.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
