using Lacoos.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.SharedKernel.Persistence;

public interface IModuleDatabase
{
    string Name { get; }

    Task MigrateAsync(CancellationToken ct);
}

internal sealed class ModuleDatabase<TContext>(TContext context) : IModuleDatabase
    where TContext : DbContext
{
    public string Name => typeof(TContext).Name;

    public Task MigrateAsync(CancellationToken ct) => context.Database.MigrateAsync(ct);
}

public static class ModulePersistence
{
    public const string ConnectionStringName = "Postgres";

    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : ModuleDbContext
    {
        services.AddDbContext<TContext>((provider, options) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException($"ConnectionStrings:{ConnectionStringName} não configurada.");
            Configure(options, connectionString, schema);
        });
        services.AddScoped<IModuleDatabase, ModuleDatabase<TContext>>();
        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, string connectionString, string schema) =>
        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", schema))
            .UseSnakeCaseNamingConvention();
}

/// <summary>Usada só pelo dotnet ef, sem iniciar a aplicação nem precisar do banco.</summary>
public abstract class ModuleDesignTimeFactory<TContext>(string schema) : IDesignTimeDbContextFactory<TContext>
    where TContext : ModuleDbContext
{
    public TContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        ModulePersistence.Configure(builder, "Host=localhost;Database=lacoos_design", schema);
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options, new WeddingContext())!;
    }
}
