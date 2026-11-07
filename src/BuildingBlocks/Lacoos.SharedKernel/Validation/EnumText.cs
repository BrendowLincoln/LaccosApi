using System.Text.Json;
using Lacoos.SharedKernel.Errors;

namespace Lacoos.SharedKernel.Validation;

/// <summary>Enums no formato do contrato, como in_progress e not_sent.</summary>
public static class EnumText
{
    public static string ToText<TEnum>(TEnum value) where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());

    /// <summary>Lê um filtro da query string. Valor desconhecido gera erro de validação.</summary>
    public static TEnum? ParseFilter<TEnum>(string? text, string field) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        foreach (var value in Enum.GetValues<TEnum>())
        {
            if (ToText(value) == text.Trim())
            {
                return value;
            }
        }

        throw new ValidationException(new Dictionary<string, string[]> { [field] = [$"Valor inválido para {field}."] });
    }
}
