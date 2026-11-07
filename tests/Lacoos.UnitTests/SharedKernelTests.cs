using Lacoos.SharedKernel.Errors;
using Lacoos.SharedKernel.Time;
using Lacoos.SharedKernel.Validation;
using Xunit;

namespace Lacoos.UnitTests;

public sealed class SharedKernelTests
{
    [Fact]
    public void Today_uses_sao_paulo_and_not_utc()
    {
        // 01:30 UTC do dia 11 ainda é 22:30 do dia 10 em São Paulo.
        var instant = new DateTimeOffset(2026, 3, 11, 1, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 3, 10), SaoPauloClock.ToSaoPauloDate(instant));
    }

    [Fact]
    public void Today_changes_at_midnight_in_sao_paulo()
    {
        var instant = new DateTimeOffset(2026, 3, 11, 3, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 3, 11), SaoPauloClock.ToSaoPauloDate(instant));
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("10.123")]
    [InlineData("1000000000000.00")]
    public void Money_rejects_invalid_values(string value)
    {
        var validator = new Validator().Money(decimal.Parse(value), "amount", "Valor");

        Assert.Throws<ValidationException>(validator.ThrowIfInvalid);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("10.5")]
    [InlineData("10.50")]
    public void Money_accepts_two_decimal_places(string value)
    {
        new Validator().Money(decimal.Parse(value), "amount", "Valor").ThrowIfInvalid();
    }

    [Fact]
    public void Search_pattern_escapes_user_wildcards()
    {
        Assert.Equal(@"%50\%\_x%", Text.ContainsPattern("50%_x"));
    }

    [Fact]
    public void Brl_uses_brazilian_separators_on_any_server_culture()
    {
        Assert.Equal("R$ 1.234,50", Text.Brl(1234.5m));
    }

    [Fact]
    public void Percent_of_zero_total_is_zero()
    {
        Assert.Equal(0, Text.Percent(10, 0));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/a")]
    [InlineData("nao-e-url")]
    public void Url_accepts_only_http_and_https(string value)
    {
        var validator = new Validator().Url(value, "link", "Link");

        Assert.Throws<ValidationException>(validator.ThrowIfInvalid);
    }
}
