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

    /// <summary>
    /// 宽松 DateTime 转换器：支持 ISO 8601 和常见中文格式（yyyy-MM-dd HH:mm:ss）
    /// </summary>
    public sealed class FlexibleDateTimeJsonConverter : JsonConverter<DateTime>
    {
        private static readonly string[] Formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
        };

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt;
                if (DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    return dt;
                throw new JsonException($"Invalid DateTime format: {text}");
            }
            if (reader.TokenType == JsonTokenType.Number)
                return DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64()).DateTime;
            throw new JsonException($"Unexpected token parsing DateTime: {reader.TokenType}");
        }
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString("o", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// 宽松可空 DateTime 转换器
    /// </summary>
    public sealed class FlexibleNullableDateTimeJsonConverter : JsonConverter<DateTime?>
    {
        private static readonly string[] Formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
        };

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;
            if (reader.TokenType == JsonTokenType.String)
            {
                var text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                    return null;
                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return dt;
                if (DateTime.TryParseExact(text, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    return dt;
                throw new JsonException($"Invalid DateTime format: {text}");
            }
            if (reader.TokenType == JsonTokenType.Number)
                return DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64()).DateTime;
            throw new JsonException($"Unexpected token parsing nullable DateTime: {reader.TokenType}");
        }
        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (!value.HasValue) { writer.WriteNullValue(); return; }
            writer.WriteStringValue(value.Value.ToString("o", CultureInfo.InvariantCulture));
        }
    }
}