namespace Lacoos.SharedKernel.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Data de hoje em America/Sao_Paulo, nunca a data UTC.</summary>
    DateOnly Today { get; }
}

public sealed class SaoPauloClock : IClock
{
    private static readonly TimeZoneInfo SaoPaulo = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly Today => ToSaoPauloDate(UtcNow);

    public static DateOnly ToSaoPauloDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, SaoPaulo).DateTime);
}
