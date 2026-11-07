namespace Lacoos.Modules.Payments.Contracts;

public sealed record SupplierPaymentSummary(
    Guid Id, string Description, decimal TotalAmount, decimal PaidAmount, decimal RemainingAmount, string Status);

public sealed record OpenInstallment(
    Guid Id,
    Guid PaymentId,
    string Description,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    decimal RemainingAmount,
    string Status,
    Guid? SupplierId);

public sealed record PaymentsOverview(
    decimal TotalPaid,
    IReadOnlyList<OpenInstallment> UpcomingInstallments,
    IReadOnlyList<OpenInstallment> OverdueInstallments);

public interface IPaymentQueries
{
    Task<bool> HasPaymentsForSupplierAsync(Guid supplierId, CancellationToken ct);

    Task<bool> HasPaymentsForBudgetItemAsync(Guid budgetItemId, CancellationToken ct);

    /// <summary>Pago por item de orçamento. Itens sem pagamentos não aparecem.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetPaidByBudgetItemAsync(CancellationToken ct);

    /// <summary>Pago por fornecedor. Fornecedores sem pagamentos não aparecem.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetPaidBySupplierAsync(CancellationToken ct);

    Task<IReadOnlyList<SupplierPaymentSummary>> GetBySupplierAsync(Guid supplierId, CancellationToken ct);

    Task<PaymentsOverview> GetOverviewAsync(int upcomingLimit, CancellationToken ct);
}
