using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace App.Utils
{
    //-------------------------------------------------
    // Json 序列化转换器
    //-------------------------------------------------
    /// <summary>
    /// 简单的日期时间格式化
    /// </summary>
    public class DateTimeConverter : JsonConverter<DateTime>
    {
        private const string DateFormat = "yyyy/MM/dd HH:mm";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is string s)
            {
                if (DateTime.TryParseExact(s, DateFormat, null, System.Globalization.DateTimeStyles.None, out var dt))
                    return dt;
                if (DateTime.TryParse(s, out dt))
                    return dt;
            }
            return reader.GetDateTime();
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(DateFormat));
        }
    }

    /// <summary>  
    /// DateTime序列化为时间戳  
    /// </summary>  
    public class TimestampConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var seconds = reader.TokenType == JsonTokenType.String
                ? long.Parse(reader.GetString() ?? "0")
                : reader.GetInt64();
            return new DateTime(1970, 1, 1).AddSeconds(seconds);
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            var n = (int)(value - new DateTime(1970, 1, 1)).TotalSeconds;
            writer.WriteNumberValue(n);
        }
    }

    /// <summary>
    /// Type 名称 Json 转化器，只保留类名和数据集名，不记录数据集版本号
    /// </summary>
    public class TypeNameConverter : JsonConverter<Type>
    {
        public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return Type.GetType(reader.GetString() ?? string.Empty) ?? typeof(object);
        }

        public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.GetShortName());
        }
    }

    /// <summary>  
    /// String Unicode 序列化, 输出为Unicode编码字符）
    /// </summary>  
    public class UnicodeConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetString() ?? string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(ToUnicode(value));
        }

        public static string ToUnicode(string str)
        {
            byte[] bts = Encoding.Unicode.GetBytes(str);
            string r = "";
            for (int i = 0; i < bts.Length; i += 2)
            {
                r += "\\u" + bts[i + 1].ToString("X").PadLeft(2, '0') + bts[i].ToString("X").PadLeft(2, '0');
            }
            return r;
        }
    }
}
