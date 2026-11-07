using Lacoos.Modules.Gifts.Domain;
using Lacoos.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lacoos.Modules.Gifts.Persistence;

internal sealed class GiftsDbContext(DbContextOptions<GiftsDbContext> options, IWeddingContext wedding)
    : ModuleDbContext(options, wedding, SchemaName)
{
    public const string SchemaName = "gifts";
    public const string GiftsTable = "gifts.gifts";

    public DbSet<GiftCategory> Categories => Set<GiftCategory>();

    public DbSet<Gift> Gifts => Set<Gift>();

    public DbSet<GiftReservation> Reservations => Set<GiftReservation>();

    public DbSet<GiftReceipt> Receipts => Set<GiftReceipt>();
}

internal sealed class GiftsDbContextFactory() : ModuleDesignTimeFactory<GiftsDbContext>(GiftsDbContext.SchemaName);

internal sealed class GiftCategoryConfiguration : IEntityTypeConfiguration<GiftCategory>
{
    public void Configure(EntityTypeBuilder<GiftCategory> builder)
    {
        builder.ToTable("gift_categories");
        builder.Property(c => c.Name).HasMaxLength(200);
    }
}

internal sealed class GiftConfiguration : IEntityTypeConfiguration<Gift>
{
    public void Configure(EntityTypeBuilder<Gift> builder)
    {
        builder.Ignore(g => g.ReservedUnits);
        builder.Ignore(g => g.ReceivedUnits);
        builder.Ignore(g => g.AvailableUnits);
        builder.Ignore(g => g.DisplayStatus);
        builder.Property(g => g.Name).HasMaxLength(200);
        builder.HasOne(g => g.Category).WithMany().HasForeignKey(g => g.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(g => g.Reservations).WithOne().HasForeignKey(r => r.GiftId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(g => g.Receipts).WithOne().HasForeignKey(r => r.GiftId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GiftReservationConfiguration : IEntityTypeConfiguration<GiftReservation>
{
    public void Configure(EntityTypeBuilder<GiftReservation> builder)
    {
        builder.ToTable("gift_reservations");
        builder.Property(r => r.PersonName).HasMaxLength(200);
        builder.HasIndex(r => r.GuestId);
    }
}

internal sealed class GiftReceiptConfiguration : IEntityTypeConfiguration<GiftReceipt>
{
    public void Configure(EntityTypeBuilder<GiftReceipt> builder)
    {
        builder.ToTable("gift_receipts");
        builder.Property(r => r.PersonName).HasMaxLength(200);
        builder.HasIndex(r => r.GuestId);
        builder.HasOne<GiftReservation>().WithMany().HasForeignKey(r => r.ReservationId).OnDelete(DeleteBehavior.Restrict);
    }
}
