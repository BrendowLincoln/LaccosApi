namespace Lacoos.Modules.Payments.Domain;

internal enum PaymentMethod
{
    Pix,
    CreditCard,
    Boleto,
    Transfer,
    Cash,
}

internal enum PaymentStatus
{
    Pending,
    Partial,
    Paid,
    Overdue,
}

internal enum TransactionType
{
    Settlement,
    Reversal,
}

internal sealed class Payment : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string Description { get; set; }

    public decimal TotalAmount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    public Guid? SupplierId { get; set; }

    public Guid? BudgetItemId { get; set; }

    public string? Notes { get; set; }

    public List<PaymentInstallment> Installments { get; set; } = [];

    public decimal PaidAmount => Installments.Sum(i => i.PaidAmount);

    public decimal RemainingAmount => Math.Max(TotalAmount - PaidAmount, 0);

    public PaymentStatus StatusOn(DateOnly today) =>
        PaymentRules.PaymentStatusOf(Installments.Select(i => i.StatusOn(today)).ToList(), PaidAmount);
}

internal sealed class PaymentInstallment : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public Guid PaymentId { get; set; }

    public int InstallmentNumber { get; set; }

    public decimal Amount { get; set; }

    public DateOnly DueDate { get; set; }

    public List<InstallmentTransaction> Transactions { get; set; } = [];

    public List<PaymentReceipt> Receipts { get; set; } = [];

    /// <summary>Calculado do histórico; nunca é salvo, para não ficar desatualizado.</summary>
    public decimal PaidAmount =>
        Transactions.Sum(t => t.Type == TransactionType.Settlement ? t.Amount : -t.Amount);

    public decimal RemainingAmount => PaymentRules.Remaining(Amount, PaidAmount);

    /// <summary>Última baixa que ainda vale, ou nulo se não há nada pago.</summary>
    public InstallmentTransaction? LatestSettlement =>
        PaidAmount <= 0
            ? null
            : Transactions.Where(t => t.Type == TransactionType.Settlement)
                .OrderBy(t => t.CreatedAt).ThenBy(t => t.Id).LastOrDefault();

    public PaymentStatus StatusOn(DateOnly today) =>
        PaymentRules.InstallmentStatusOf(Amount, PaidAmount, DueDate, today);
}

/// <summary>Histórico imutável: baixas somam e estornos subtraem. O estorno não apaga a baixa.</summary>
internal sealed class InstallmentTransaction : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public Guid InstallmentId { get; set; }

    public TransactionType Type { get; set; }

    public decimal Amount { get; set; }

    public DateOnly PaidAt { get; set; }

    public PaymentMethod? PaymentMethod { get; set; }

    public string? Notes { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

internal sealed class PaymentReceipt : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public Guid InstallmentId { get; set; }

    public Guid? TransactionId { get; set; }

    public required string FileName { get; set; }

    public required string FileKey { get; set; }

    public required string ContentType { get; set; }

    public long Size { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
}
