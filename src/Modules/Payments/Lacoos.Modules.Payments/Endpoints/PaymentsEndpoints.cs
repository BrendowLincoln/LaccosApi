using Lacoos.Modules.Payments.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Lacoos.Modules.Payments.Endpoints;

internal static class PaymentsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var w = app.MapWeddingGroup("Pagamentos");
        var payments = w.MapGroup("/payments");

        payments.MapGet("", ([AsParameters] PaymentFilter filter, PaymentService service, CancellationToken ct) =>
            service.ListAsync(filter, ct));

        payments.MapPost("", async (SavePaymentRequest request, PaymentService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.CreateAsync(request, ct)));

        payments.MapGet("/{id:guid}", (Guid id, PaymentService service, CancellationToken ct) => service.GetAsync(id, ct));

        payments.MapPut("/{id:guid}", (Guid id, SavePaymentRequest request, PaymentService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));

        payments.MapDelete("/{id:guid}", async (Guid id, PaymentService service, CancellationToken ct) =>
        {
            await service.DeleteAsync(id, ct);
            return TypedResults.NoContent();
        });

        payments.MapPatch("/{id:guid}/installments/{number:int}", (Guid id, int number, UpdateInstallmentRequest request, PaymentService service, CancellationToken ct) =>
            service.UpdateInstallmentAsync(id, number, request, ct));

        var installments = w.MapGroup("/installments/{id:guid}");

        installments.MapGet("/settlements", (Guid id, PaymentService service, CancellationToken ct) =>
            service.ListTransactionsAsync(id, ct));

        installments.MapPost("/settlements", async (Guid id, SettleRequest request, PaymentService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.SettleAsync(id, request, ct)));

        installments.MapPost("/reversal", async (Guid id, ReversalRequest request, PaymentService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.ReverseAsync(id, request, ct)));

        installments.MapPost("/receipts", async (Guid id, [FromForm] Guid? transactionId, IFormFile? file, PaymentService service, CancellationToken ct) =>
            TypedResults.Created((string?)null, await service.AddReceiptAsync(id, transactionId, file, ct))).DisableAntiforgery();

        var receipts = w.MapGroup("/receipts");

        receipts.MapGet("/{id:guid}", (Guid id, PaymentService service, CancellationToken ct) =>
            service.DownloadReceiptAsync(id, ct));

        receipts.MapDelete("/{id:guid}", async (Guid id, PaymentService service, CancellationToken ct) =>
        {
            await service.DeleteReceiptAsync(id, ct);
            return TypedResults.NoContent();
        });
    }
}
