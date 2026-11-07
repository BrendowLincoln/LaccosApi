using Lacoos.Modules.Payments.Domain;
using Xunit;

namespace Lacoos.UnitTests;

public sealed class PaymentRulesTests
{
    private static readonly DateOnly Today = new(2026, 6, 15);

    [Theory]
    [InlineData("100.00", 3, "33.33", "33.34")]
    [InlineData("200.00", 3, "66.66", "66.68")]
    [InlineData("0.10", 3, "0.03", "0.04")]
    [InlineData("1000.00", 4, "250.00", "250.00")]
    [InlineData("99.99", 7, "14.28", "14.31")]
    public void Split_truncates_and_leaves_the_remainder_in_the_last(string total, int count, string regular, string last)
    {
        var amounts = PaymentRules.SplitAmount(decimal.Parse(total), count);

        Assert.Equal(count, amounts.Count);
        Assert.All(amounts.SkipLast(1), amount => Assert.Equal(decimal.Parse(regular), amount));
        Assert.Equal(decimal.Parse(last), amounts[^1]);
    }

    [Theory]
    [InlineData("0.00")]
    [InlineData("0.01")]
    [InlineData("1234.56")]
    [InlineData("999999999.99")]
    public void Split_always_sums_to_the_total(string total)
    {
        var value = decimal.Parse(total);

        for (var count = PaymentRules.MinInstallments; count <= PaymentRules.MaxInstallments; count++)
        {
            var amounts = PaymentRules.SplitAmount(value, count);

            Assert.Equal(value, amounts.Sum());
            Assert.All(amounts, amount => Assert.Equal(decimal.Round(amount, 2), amount));
        }
    }

    [Fact]
    public void Due_dates_clamp_to_the_last_day_of_february_in_a_common_year()
    {
        var dates = PaymentRules.MonthlyDueDates(new DateOnly(2027, 1, 31), 4);

        Assert.Equal(
            [new DateOnly(2027, 1, 31), new DateOnly(2027, 2, 28), new DateOnly(2027, 3, 31), new DateOnly(2027, 4, 30)],
            dates);
    }

    [Fact]
    public void Due_dates_clamp_to_february_29_in_a_leap_year()
    {
        var dates = PaymentRules.MonthlyDueDates(new DateOnly(2028, 1, 31), 2);

        Assert.Equal(new DateOnly(2028, 2, 29), dates[1]);
    }

    [Fact]
    public void Due_dates_cross_the_year()
    {
        var dates = PaymentRules.MonthlyDueDates(new DateOnly(2026, 11, 30), 4);

        Assert.Equal(
            [new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 30), new DateOnly(2027, 1, 30), new DateOnly(2027, 2, 28)],
            dates);
    }

    [Theory]
    [InlineData("100", "100", -10, "Paid")]
    [InlineData("100", "100", 10, "Paid")]
    [InlineData("100", "40", -1, "Overdue")]
    [InlineData("100", "0", -1, "Overdue")]
    [InlineData("100", "40", 0, "Partial")]
    [InlineData("100", "40", 5, "Partial")]
    [InlineData("100", "0", 0, "Pending")]
    [InlineData("100", "0", 5, "Pending")]
    public void Installment_status_follows_the_documented_order(string amount, string paid, int dueInDays, string expected)
    {
        var status = PaymentRules.InstallmentStatusOf(decimal.Parse(amount), decimal.Parse(paid), Today.AddDays(dueInDays), Today);

        Assert.Equal(Enum.Parse<PaymentStatus>(expected), status);
    }

    [Fact]
    public void Payment_status_is_paid_only_when_all_installments_are_paid()
    {
        Assert.Equal(PaymentStatus.Paid, PaymentRules.PaymentStatusOf([PaymentStatus.Paid, PaymentStatus.Paid], 100));
        Assert.NotEqual(PaymentStatus.Paid, PaymentRules.PaymentStatusOf([PaymentStatus.Paid, PaymentStatus.Pending], 50));
    }

    [Fact]
    public void Payment_status_overdue_wins_over_partial()
    {
        Assert.Equal(PaymentStatus.Overdue, PaymentRules.PaymentStatusOf([PaymentStatus.Paid, PaymentStatus.Overdue, PaymentStatus.Pending], 50));
    }

    [Fact]
    public void Payment_status_is_partial_with_any_value_paid()
    {
        Assert.Equal(PaymentStatus.Partial, PaymentRules.PaymentStatusOf([PaymentStatus.Paid, PaymentStatus.Pending], 50));
    }

    [Fact]
    public void Payment_status_is_pending_with_nothing_paid()
    {
        Assert.Equal(PaymentStatus.Pending, PaymentRules.PaymentStatusOf([PaymentStatus.Pending, PaymentStatus.Pending], 0));
    }

    [Theory]
    [InlineData("100", "30", "70")]
    [InlineData("100", "100", "0")]
    [InlineData("100", "120", "0")]
    public void Remaining_is_never_negative(string total, string paid, string expected)
    {
        Assert.Equal(decimal.Parse(expected), PaymentRules.Remaining(decimal.Parse(total), decimal.Parse(paid)));
    }

    [Fact]
    public void Reversal_subtracts_from_paid_and_keeps_the_history()
    {
        var installment = new PaymentInstallment { Amount = 100, DueDate = Today.AddDays(1) };
        installment.Transactions.Add(new InstallmentTransaction { Type = TransactionType.Settlement, Amount = 60 });
        installment.Transactions.Add(new InstallmentTransaction { Type = TransactionType.Reversal, Amount = 60 });

        Assert.Equal(0, installment.PaidAmount);
        Assert.Equal(100, installment.RemainingAmount);
        Assert.Null(installment.LatestSettlement);
        Assert.Equal(PaymentStatus.Pending, installment.StatusOn(Today));
        Assert.Equal(2, installment.Transactions.Count);
    }

    [Fact]
    public void Status_changes_with_the_date_without_any_stored_value()
    {
        var installment = new PaymentInstallment { Amount = 100, DueDate = Today };

        Assert.Equal(PaymentStatus.Pending, installment.StatusOn(Today));
        Assert.Equal(PaymentStatus.Overdue, installment.StatusOn(Today.AddDays(1)));
    }
}
