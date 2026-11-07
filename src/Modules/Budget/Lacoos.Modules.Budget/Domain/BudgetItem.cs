namespace Lacoos.Modules.Budget.Domain;

internal enum BudgetItemStatus
{
    Planned,
    Contracted,
    Cancelled,
}

/// <summary>Valor oficial do orçamento total do casamento.</summary>
internal sealed class BudgetSettings : IWeddingScoped
{
    public Guid WeddingId { get; set; }

    public decimal TotalAmount { get; set; }
}

internal sealed class BudgetCategory : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal PlannedAmount { get; set; }
}

internal sealed class BudgetItem : IWeddingScoped
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WeddingId { get; set; }

    public Guid CategoryId { get; set; }

    public BudgetCategory? Category { get; set; }

    public Guid? SupplierId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public decimal PlannedAmount { get; set; }

    public decimal ContractedAmount { get; set; }

    public BudgetItemStatus Status { get; set; } = BudgetItemStatus.Planned;

    /// <summary>Itens cancelados não contam como valor comprometido.</summary>
    public decimal CommittedAmount => Status == BudgetItemStatus.Cancelled ? 0 : ContractedAmount;
}

internal static class BudgetMath
{
    /// <summary>Sem valor definido, o orçamento total é a soma das categorias.</summary>
    public static decimal TotalBudget(decimal? definedTotal, decimal categoriesPlanned) =>
        definedTotal ?? categoriesPlanned;
}
