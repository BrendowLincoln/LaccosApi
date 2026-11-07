namespace Lacoos.Modules.Payments.Domain;

/// <summary>Regras puras de parcelas e status.</summary>
internal static class PaymentRules
{
    public const int MinInstallments = 1;
    public const int MaxInstallments = 24;

    /// <summary>
    /// Divide igualmente, truncando em 2 casas, e deixa o resto na última parcela.
    /// Truncar garante que a última nunca fica menor que as demais.
    /// </summary>
    public static IReadOnlyList<decimal> SplitAmount(decimal total, int count)
    {
        var baseAmount = Math.Truncate(total * 100 / count) / 100;
        var amounts = Enumerable.Repeat(baseAmount, count).ToArray();
        amounts[^1] = total - (baseAmount * (count - 1));
        return amounts;
    }

    /// <summary>
    /// Mensal sempre a partir do primeiro vencimento: 31/01 gera 28/02 e depois 31/03.
    /// Somar ao mês anterior perderia o dia original.
    /// </summary>
    public static IReadOnlyList<DateOnly> MonthlyDueDates(DateOnly first, int count) =>
        Enumerable.Range(0, count).Select(first.AddMonths).ToList();

    public static decimal Remaining(decimal total, decimal paid) => Math.Max(total - paid, 0);

    public static PaymentStatus InstallmentStatusOf(decimal amount, decimal paid, DateOnly dueDate, DateOnly today)
    {
        if (paid >= amount)
        {
            return PaymentStatus.Paid;
        }

        if (dueDate < today)
        {
            return PaymentStatus.Overdue;
        }

        return paid > 0 ? PaymentStatus.Partial : PaymentStatus.Pending;
    }

    public static PaymentStatus PaymentStatusOf(IReadOnlyCollection<PaymentStatus> installments, decimal paid)
    {
        if (installments.All(s => s == PaymentStatus.Paid))
        {
            return PaymentStatus.Paid;
        }

        if (installments.Any(s => s == PaymentStatus.Overdue))
        {
            return PaymentStatus.Overdue;
        }

        return paid > 0 ? PaymentStatus.Partial : PaymentStatus.Pending;
    }
}
