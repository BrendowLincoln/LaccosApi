using Lacoos.Modules.Budget.Contracts;
using Lacoos.Modules.Checklist.Contracts;
using Lacoos.Modules.Guests.Contracts;
using Lacoos.Modules.Payments.Contracts;
using Lacoos.Modules.Suppliers.Contracts;
using Lacoos.Modules.Weddings.Contracts;
using Lacoos.SharedKernel.Modules;
using Lacoos.SharedKernel.Tenancy;
using Lacoos.SharedKernel.Time;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lacoos.Modules.Dashboard;

internal sealed record FinancialCard(decimal TotalBudget, decimal Committed, decimal Paid, decimal Balance, decimal CommittedPercent);

internal sealed record InstallmentCard(
    Guid Id,
    Guid PaymentId,
    string Description,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal RemainingAmount,
    string Status,
    string? SupplierName);

internal sealed record TaskCard(
    Guid Id, string Title, DateOnly DueDate, string Priority, string Status, Guid? AssignedToMemberId, string? AssignedToMemberName);

internal sealed record DashboardResponse(
    WeddingInfo Wedding,
    int DaysRemaining,
    ChecklistSummary Checklist,
    FinancialCard Financial,
    GuestTotals Guests,
    IReadOnlyList<InstallmentCard> UpcomingInstallments,
    IReadOnlyList<InstallmentCard> OverdueInstallments,
    IReadOnlyList<TaskCard> UpcomingTasks);

/// <summary>Compõe os dados só pelos contratos públicos dos módulos. Não tem tabelas próprias.</summary>
internal sealed class DashboardService(
    IWeddingDirectory weddings,
    IChecklistQueries checklist,
    IBudgetQueries budget,
    IPaymentQueries payments,
    IGuestQueries guests,
    ISupplierQueries suppliers,
    IClock clock)
{
    private const int UpcomingInstallmentsLimit = 4;
    private const int UpcomingTasksLimit = 5;
    private const int TaskWindowDays = 30;

    public async Task<DashboardResponse> GetAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var wedding = await weddings.GetCurrentAsync(ct);
        var summary = await checklist.GetSummaryAsync(ct);
        var financials = await budget.GetFinancialsAsync(ct);
        var overview = await payments.GetOverviewAsync(UpcomingInstallmentsLimit, ct);
        var guestTotals = await guests.GetTotalsAsync(ct);
        var tasks = await checklist.GetUpcomingTasksAsync(today.AddDays(TaskWindowDays), UpcomingTasksLimit, ct);

        var supplierIds = overview.UpcomingInstallments.Concat(overview.OverdueInstallments)
            .Where(i => i.SupplierId is not null)
            .Select(i => i.SupplierId!.Value)
            .Distinct()
            .ToList();
        var supplierNames = await suppliers.GetNamesAsync(supplierIds, ct);

        var memberIds = tasks.Where(t => t.AssignedToMemberId is not null).Select(t => t.AssignedToMemberId!.Value).Distinct().ToList();
        var memberNames = await weddings.GetMemberNamesAsync(memberIds, ct);

        InstallmentCard ToCard(OpenInstallment i) => new(
            i.Id, i.PaymentId, i.Description, i.InstallmentNumber, i.DueDate, i.Amount, i.RemainingAmount, i.Status,
            i.SupplierId is { } id ? supplierNames.GetValueOrDefault(id) : null);

        return new DashboardResponse(
            wedding,
            Math.Max(wedding.WeddingDate.DayNumber - today.DayNumber, 0),
            summary,
            new FinancialCard(financials.TotalBudget, financials.Committed, overview.TotalPaid, financials.Balance, financials.CommittedPercent),
            guestTotals,
            overview.UpcomingInstallments.Select(ToCard).ToList(),
            overview.OverdueInstallments.Select(ToCard).ToList(),
            tasks.Select(t => new TaskCard(
                t.Id, t.Title, t.DueDate, t.Priority, t.Status, t.AssignedToMemberId,
                t.AssignedToMemberId is { } id ? memberNames.GetValueOrDefault(id) : null)).ToList());
    }
}

public sealed class DashboardModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration) =>
        services.AddScoped<DashboardService>();

    public void MapEndpoints(IEndpointRouteBuilder app) =>
        app.MapWeddingGroup("Dashboard")
            .MapGet("/dashboard", (DashboardService service, CancellationToken ct) => service.GetAsync(ct));
}
