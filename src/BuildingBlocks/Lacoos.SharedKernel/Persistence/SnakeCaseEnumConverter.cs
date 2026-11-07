using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Lacoos.SharedKernel.Persistence;

/// <summary>Salva enums com o mesmo texto do contrato JSON, como in_progress.</summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ToText(value),
    text => FromText(text))
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Names =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString()));

    private static readonly Dictionary<string, TEnum> Values = Names.ToDictionary(p => p.Value, p => p.Key);

    private static string ToText(TEnum value) => Names[value];

    private static TEnum FromText(string text) => Values[text];
}
