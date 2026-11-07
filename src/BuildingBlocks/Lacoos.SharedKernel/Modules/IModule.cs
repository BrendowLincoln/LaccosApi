using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.SharedKernel.Modules;

/// <summary>Único ponto público de um módulo para o host.</summary>
public interface IModule
{
    void Register(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder app);

    /// <summary>Middlewares que o módulo precisa, executados após a autenticação.</summary>
    void ConfigurePipeline(IApplicationBuilder app)
    {
    }
}

/// <summary>Carga de dados de exemplo de um módulo, usada só em desenvolvimento.</summary>
public interface IModuleSeeder
{
    int Order { get; }

    Task SeedAsync(DemoSeedOptions options, CancellationToken ct);
}

public sealed record DemoSeedOptions(string DemoPassword);

/// <summary>IDs fixos que permitem aos módulos relacionar os dados de exemplo.</summary>
public static class DemoIds
{
    public static readonly Guid OwnerUser = new("0194f000-0000-7000-8000-000000000001");
    public static readonly Guid OrganizerUser = new("0194f000-0000-7000-8000-000000000002");
    public static readonly Guid ViewerUser = new("0194f000-0000-7000-8000-000000000003");
    public static readonly Guid Wedding = new("0194f000-0000-7000-8000-000000000010");
    public static readonly Guid OwnerMember = new("0194f000-0000-7000-8000-000000000011");
    public static readonly Guid OrganizerMember = new("0194f000-0000-7000-8000-000000000012");
    public static readonly Guid ViewerMember = new("0194f000-0000-7000-8000-000000000013");
    public static readonly Guid PhotoSupplier = new("0194f000-0000-7000-8000-000000000020");
    public static readonly Guid VenueSupplier = new("0194f000-0000-7000-8000-000000000021");
    public static readonly Guid BuffetSupplier = new("0194f000-0000-7000-8000-000000000022");
    public static readonly Guid PhotoBudgetItem = new("0194f000-0000-7000-8000-000000000030");
    public static readonly Guid VenueBudgetItem = new("0194f000-0000-7000-8000-000000000031");
    public static readonly Guid BuffetBudgetItem = new("0194f000-0000-7000-8000-000000000032");
    public static readonly Guid FirstGuest = new("0194f000-0000-7000-8000-000000000040");
    public static readonly Guid SecondGuest = new("0194f000-0000-7000-8000-000000000041");
}
