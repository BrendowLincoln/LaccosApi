using Lacoos.Modules.Weddings.Domain;
using Lacoos.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lacoos.Modules.Weddings.Persistence;

internal sealed class WeddingsDbContext(DbContextOptions<WeddingsDbContext> options, IWeddingContext wedding)
    : ModuleDbContext(options, wedding, SchemaName)
{
    public const string SchemaName = "weddings";
    public const string WeddingsTable = "weddings.weddings";

    public DbSet<Wedding> Weddings => Set<Wedding>();

    public DbSet<WeddingMember> Members => Set<WeddingMember>();

    public DbSet<MemberInvitation> Invitations => Set<MemberInvitation>();
}

internal sealed class WeddingsDbContextFactory() : ModuleDesignTimeFactory<WeddingsDbContext>(WeddingsDbContext.SchemaName);

internal sealed class WeddingConfiguration : IEntityTypeConfiguration<Wedding>
{
    public void Configure(EntityTypeBuilder<Wedding> builder)
    {
        builder.Property(w => w.Name).HasMaxLength(200);
        builder.Property(w => w.Partner1).HasMaxLength(200);
        builder.Property(w => w.Partner2).HasMaxLength(200);
        builder.Property(w => w.City).HasMaxLength(200);
        builder.Property(w => w.State).HasMaxLength(100);
        builder.Property(w => w.Venue).HasMaxLength(300);
    }
}

internal sealed class WeddingMemberConfiguration : IEntityTypeConfiguration<WeddingMember>
{
    public void Configure(EntityTypeBuilder<WeddingMember> builder)
    {
        builder.ToTable("wedding_members");
        builder.Property(m => m.Name).HasMaxLength(200);
        builder.Property(m => m.Email).HasMaxLength(320);
        builder.Property(m => m.NormalizedEmail).HasMaxLength(320);
        builder.HasIndex(m => new { m.WeddingId, m.NormalizedEmail }).IsUnique();
        builder.HasIndex(m => new { m.WeddingId, m.UserId }).IsUnique().HasFilter("user_id IS NOT NULL");
        builder.HasIndex(m => m.UserId);
        builder.HasOne<Wedding>().WithMany().HasForeignKey(m => m.WeddingId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MemberInvitationConfiguration : IEntityTypeConfiguration<MemberInvitation>
{
    public void Configure(EntityTypeBuilder<MemberInvitation> builder)
    {
        builder.ToTable("member_invitations");
        builder.HasIndex(i => i.TokenHash).IsUnique();
        builder.HasIndex(i => i.MemberId).IsUnique();
        builder.HasOne<WeddingMember>().WithMany().HasForeignKey(i => i.MemberId).OnDelete(DeleteBehavior.Cascade);
    }
}
