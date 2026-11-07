using Lacoos.Modules.Suppliers.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Lacoos.Modules.Suppliers.Endpoints;

internal static class SuppliersEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var suppliers = app.MapWeddingGroup("Fornecedores").MapGroup("/suppliers");

        suppliers.MapGet("", ([AsParameters] SupplierFilter filter, SupplierAppService service, CancellationToken ct) =>
            service.ListAsync(filter, ct));

        suppliers.MapPost("", async (SaveSupplierRequest request, SupplierAppService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateAsync(request, ct)));

        suppliers.MapGet("/{id:guid}", (Guid id, SupplierAppService service, CancellationToken ct) => service.GetAsync(id, ct));

        suppliers.MapPut("/{id:guid}", (Guid id, SaveSupplierRequest request, SupplierAppService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));

        suppliers.MapDelete("/{id:guid}", async (Guid id, SupplierAppService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        suppliers.MapGet("/{id:guid}/payments", (Guid id, SupplierAppService service, CancellationToken ct) =>
            service.ListPaymentsAsync(id, ct));

        var services = suppliers.MapGroup("/{supplierId:guid}/services");

        services.MapGet("", (Guid supplierId, SupplierAppService service, CancellationToken ct) =>
            service.ListServicesAsync(supplierId, ct));

        services.MapPost("", async (Guid supplierId, SaveServiceRequest request, SupplierAppService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateServiceAsync(supplierId, request, ct)));

        services.MapGet("/{id:guid}", (Guid supplierId, Guid id, SupplierAppService service, CancellationToken ct) =>
            service.GetServiceAsync(supplierId, id, ct));

        services.MapPut("/{id:guid}", (Guid supplierId, Guid id, SaveServiceRequest request, SupplierAppService service, CancellationToken ct) =>
            service.UpdateServiceAsync(supplierId, id, request, ct));

        services.MapDelete("/{id:guid}", async (Guid supplierId, Guid id, SupplierAppService service, CancellationToken ct) =>
        {
            await service.DeleteServiceAsync(supplierId, id, ct);
            return TypedResults.NoContent();
        });

        var contracts = suppliers.MapGroup("/{supplierId:guid}/contracts");

        contracts.MapGet("", (Guid supplierId, SupplierAppService service, CancellationToken ct) =>
            service.ListContractsAsync(supplierId, ct));

        contracts.MapPost("", async (Guid supplierId, SaveContractRequest request, SupplierAppService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateContractAsync(supplierId, request, ct)));

        contracts.MapGet("/{id:guid}", (Guid supplierId, Guid id, SupplierAppService service, CancellationToken ct) =>
            service.GetContractAsync(supplierId, id, ct));

        contracts.MapPut("/{id:guid}", (Guid supplierId, Guid id, SaveContractRequest request, SupplierAppService service, CancellationToken ct) =>
            service.UpdateContractAsync(supplierId, id, request, ct));

        contracts.MapDelete("/{id:guid}", async (Guid supplierId, Guid id, SupplierAppService service, CancellationToken ct) =>
        {
            await service.DeleteContractAsync(supplierId, id, ct);
            return TypedResults.NoContent();
        });

        contracts.MapPut("/{id:guid}/file", (Guid supplierId, Guid id, IFormFile? file, SupplierAppService service, CancellationToken ct) =>
            service.UploadContractFileAsync(supplierId, id, file, ct)).DisableAntiforgery();

        contracts.MapGet("/{id:guid}/file", (Guid supplierId, Guid id, SupplierAppService service, CancellationToken ct) =>
            service.DownloadContractFileAsync(supplierId, id, ct));
    }
}
