using Lacoos.Modules.Identity.Domain;
using Lacoos.SharedKernel.Persistence;
using Lacoos.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lacoos.Modules.Identity.Persistence;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, IWeddingContext wedding)
    : ModuleDbContext(options, wedding, SchemaName)
{
    public const string SchemaName = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
}

internal sealed class IdentityDbContextFactory() : ModuleDesignTimeFactory<IdentityDbContext>(IdentityDbContext.SchemaName);

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Name).HasMaxLength(200);
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.NormalizedEmail).HasMaxLength(320);
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
    }
}

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.HasIndex(s => s.TokenHash).IsUnique();
        builder.HasIndex(s => s.UserId);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
