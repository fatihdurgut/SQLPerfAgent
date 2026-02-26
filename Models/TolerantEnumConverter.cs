using System.Text.Json;
using System.Text.Json.Serialization;

namespace SQLPerfAgent.Models;

/// <summary>
/// A JSON converter that gracefully handles invalid enum values by using a default fallback.
/// </summary>
public class TolerantEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    private readonly TEnum _defaultValue;

    public TolerantEnumConverter(TEnum defaultValue)
    {
        _defaultValue = defaultValue;
    }

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (value is not null && Enum.TryParse<TEnum>(value, ignoreCase: true, out var result))
            {
                return result;
            }
            
            // Invalid enum value - use default
            return _defaultValue;
        }

        if (reader.TokenType == JsonTokenType.Number)
        {
            var intValue = reader.GetInt32();
            if (Enum.IsDefined(typeof(TEnum), intValue))
            {
                return (TEnum)(object)intValue;
            }
            
            // Invalid enum value - use default
            return _defaultValue;
        }

        return _defaultValue;
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Factory for creating enum converters with default values.
/// </summary>
public static class TolerantEnumConverterFactory
{
    public static JsonConverter<RecommendationCategory> CreateCategoryConverter() =>
        new TolerantEnumConverter<RecommendationCategory>(RecommendationCategory.Configuration);

    public static JsonConverter<RecommendationSeverity> CreateSeverityConverter() =>
        new TolerantEnumConverter<RecommendationSeverity>(RecommendationSeverity.Informational);
}
