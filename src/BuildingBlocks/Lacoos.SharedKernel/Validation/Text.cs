using System.Globalization;

namespace Lacoos.SharedKernel.Validation;

public static class Text
{
    private static readonly NumberFormatInfo BrlFormat = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    /// <summary>Remove espaços nas pontas e transforma texto vazio em nulo.</summary>
    public static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Padrão ILIKE "contém", com curingas do usuário escapados.</summary>
    public static string ContainsPattern(string search)
    {
        var escaped = search.Trim()
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
        return $"%{escaped}%";
    }

    /// <summary>Valor para mensagens, como R$ 1.234,56, sem depender da cultura do servidor.</summary>
    public static string Brl(decimal value) => $"R$ {value.ToString("N2", BrlFormat)}";

    public static decimal Percent(decimal part, decimal total) =>
        total <= 0 ? 0 : decimal.Round(part / total * 100, 2, MidpointRounding.AwayFromZero);
}
