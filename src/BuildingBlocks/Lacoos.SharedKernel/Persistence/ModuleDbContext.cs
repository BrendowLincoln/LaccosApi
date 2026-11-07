using System.Linq.Expressions;
using System.Reflection;
using Lacoos.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Lacoos.SharedKernel.Persistence;

/// <summary>
/// Base dos DbContexts de módulo: schema próprio, filtro global por casamento,
/// preenchimento automático de WeddingId e enums salvos em snake_case.
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, IWeddingContext wedding, string schema)
    : DbContext(options)
{
    private static readonly MethodInfo ApplyFilterMethod = typeof(ModuleDbContext)
        .GetMethod(nameof(ApplyWeddingFilter), BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Sem casamento na requisição, o filtro usa um ID vazio e nada é retornado.</summary>
    public Guid CurrentWeddingId => wedding.WeddingId ?? Guid.Empty;

    protected override void ConfigureConventions(ModelConfigurationBuilder builder) =>
        builder.Properties<decimal>().HavePrecision(14, 2);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(schema);
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        foreach (var entity in modelBuilder.Model.GetEntityTypes().ToList())
        {
            if (typeof(IWeddingScoped).IsAssignableFrom(entity.ClrType))
            {
                ApplyFilterMethod.MakeGenericMethod(entity.ClrType).Invoke(this, [modelBuilder]);
            }

            foreach (var property in entity.GetProperties())
            {
                ApplyEnumConverter(property);
            }
        }
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<IWeddingScoped>().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity.WeddingId == Guid.Empty)
            {
                entry.Entity.WeddingId = wedding.WeddingId
                    ?? throw new InvalidOperationException("Gravação sem casamento definido na requisição.");
            }
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyWeddingFilter<T>(ModelBuilder modelBuilder) where T : class, IWeddingScoped
    {
        Expression<Func<T, bool>> filter = e => e.WeddingId == CurrentWeddingId;
        modelBuilder.Entity<T>().HasQueryFilter(filter);
        modelBuilder.Entity<T>().HasIndex(e => e.WeddingId);
    }

    private static void ApplyEnumConverter(IMutableProperty property)
    {
        var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
        if (!type.IsEnum)
        {
            return;
        }

        var converter = (ValueConverter)Activator.CreateInstance(typeof(SnakeCaseEnumConverter<>).MakeGenericType(type))!;
        property.SetValueConverter(converter);
    }
}
