using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared;

public class DateOnlyJsonConverter : JsonConverter<DateOnly>
{
    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return DateOnly.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
    {
        var isoDate = value.ToString("O");
        writer.WriteStringValue(isoDate);
    }
}

public class JsonOptions
{
    public static JsonSerializerOptions DateOnly= new()
    {
        WriteIndented = true,
        Converters =
        {
            new DateOnlyJsonConverter()
        }
    };
}