using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App
{
    /// <summary>
    /// Int64 字符串转换器
    /// </summary>
    public sealed class Int64ToStringJsonConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    return value;
                throw new JsonException($"Invalid Int64 string value: {text}");
            }

            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetInt64();

            throw new JsonException($"Unexpected token parsing Int64: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
        }
    }


    /// <summary>
    /// 可空 Int64 字符串转换器
    /// </summary>
    public sealed class NullableInt64ToStringJsonConverter : JsonConverter<long?>
    {
        public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    return value;
                throw new JsonException($"Invalid nullable Int64 string value: {text}");
            }

            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetInt64();

            throw new JsonException($"Unexpected token parsing nullable Int64: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// UInt64 字符串转换器
    /// </summary>
    public sealed class UInt64ToStringJsonConverter : JsonConverter<ulong>
    {
        public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    return value;
                throw new JsonException($"Invalid UInt64 string value: {text}");
            }

            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetUInt64();

            throw new JsonException($"Unexpected token parsing UInt64: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// 可空 UInt64 字符串转换器
    /// </summary>
    public sealed class NullableUInt64ToStringJsonConverter : JsonConverter<ulong?>
    {
        public override ulong? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    return value;
                throw new JsonException($"Invalid nullable UInt64 string value: {text}");
            }

            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetUInt64();

            throw new JsonException($"Unexpected token parsing nullable UInt64: {reader.TokenType}");
        }

        public override void Write(Utf8JsonWriter writer, ulong? value, JsonSerializerOptions options)
        {
            if (!value.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
        }
    }
}

