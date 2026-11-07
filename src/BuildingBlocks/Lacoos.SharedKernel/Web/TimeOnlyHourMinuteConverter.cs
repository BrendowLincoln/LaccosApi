using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lacoos.SharedKernel.Web;

/// <summary>Horário do casamento no contrato: HH:mm.</summary>
public sealed class TimeOnlyHourMinuteConverter : JsonConverter<TimeOnly>
{
    private const string Format = "HH:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimeOnly.TryParseExact(reader.GetString(), Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : throw new JsonException("Horário deve estar no formato HH:mm.");

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
