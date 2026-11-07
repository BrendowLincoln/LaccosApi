using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Persistence;

namespace Lacoos.Api.Infrastructure;

internal static class StartupTasks
{
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken ct)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        foreach (var database in scope.ServiceProvider.GetServices<IModuleDatabase>())
        {
            logger.LogInformation("Aplicando migrations de {Module}", database.Name);
            await database.MigrateAsync(ct);
        }
    }

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken ct)
    {
        var password = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Seed:DemoPassword é obrigatório quando Seed:Enabled é true.");
        }

        await using var scope = services.CreateAsyncScope();
        var seeders = scope.ServiceProvider.GetServices<IModuleSeeder>().OrderBy(s => s.Order);
        foreach (var seeder in seeders)
        {
            await seeder.SeedAsync(new DemoSeedOptions(password), ct);
        }
    }
}
