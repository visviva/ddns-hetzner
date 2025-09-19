using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DdnsHetzner;

public class DateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly string[] DateFormats =
    {
        "yyyy-MM-dd HH:mm:ss +0000 UTC",           // Standard format
        "yyyy-MM-dd HH:mm:ss.fff +0000 UTC",      // With milliseconds
        "yyyy-MM-dd HH:mm:ss.ffffff +0000 UTC"    // With microseconds
    };

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            string? dateString = reader.GetString();

            if (string.IsNullOrEmpty(dateString))
                return null;

            foreach (var format in DateFormats)
            {
                if (DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result))
                {
                    return result;
                }
            }

            if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime fallbackResult))
            {
                return fallbackResult;
            }
        }

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value.HasValue)
        {
            writer.WriteStringValue(value.Value.ToString(DateFormats[0], CultureInfo.InvariantCulture));
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}