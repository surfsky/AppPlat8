using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
//using System.Drawing;
//using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;


namespace App.Utils
{
    /// <summary>
    /// Json 相关的操作
    /// </summary>
    public static class JsonHelper
    {
        //------------------------------------------------
        // JSON
        //------------------------------------------------
        /// <summary>OBJECT -> JSON</summary>
        public static string ToJson(this object obj, JsonSerializerOptions settings = null)
        {
            if (obj == null)
                return "";
            settings = settings ?? GetDefaultJsonSettings();
            return JsonSerializer.Serialize(obj, settings);
        }


        /// <summary>JSON -> OBJECT (注意该方法无法解析简单数据类型)</summary>
        /// <param name="ignoreException">是否忽略异常。如果为true，解析失败时会返回null</param>
        public static object ParseJson(this string txt, Type type, bool ignoreException = false, JsonSerializerOptions settings = null)
        {
            try
            {
                settings = settings ?? GetDefaultJsonSettings();
                return JsonSerializer.Deserialize(txt, type, settings);
            }
            catch (Exception)
            {
                if (ignoreException)
                    return null;
                throw;
            }
        }

        /// <summary>JSON -> OBJECT</summary>
        /// <param name="ignoreException">是否忽略异常。如果为true，解析失败时会返回null</param>
        public static T ParseJson<T>(this string txt, bool ignoreException = false, JsonSerializerOptions settings = null)
        {
            try
            {
                settings = settings ?? GetDefaultJsonSettings();
                return JsonSerializer.Deserialize<T>(txt, settings);
            }
            catch (Exception)
            {
                if (ignoreException)
                    return default(T);
                throw;
            }
        }

        //--------------------------------------------
        // JObject
        //--------------------------------------------
        /// <summary>Json 字符串转换为 JObject 对象。获取节点值可用： var name = o["Name1"]["Name2"].ToString(); var age = (int)o["age"];</summary>
        public static JsonObject ParseJObject(this string json)
        {
            var normalized = NormalizeJsonLikeInput(json);
            return JsonNode.Parse(normalized) as JsonObject ?? new JsonObject();
        }

        private static string NormalizeJsonLikeInput(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "{}";

            var normalized = json.Replace('\'', '"');
            normalized = Regex.Replace(
                normalized,
                @"(?<=\{|,)\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:",
                m => $"\"{m.Groups["key"].Value}\":"
            );
            return normalized;
        }

        /// <summary>将对象转化为JObject对象（将忽略空属性）</summary>
        /// <param name="settings">定义json序列化时的格式</param>
        public static JsonObject AsJObject(this object o, JsonSerializerOptions settings=null)
        {
            if (o is JsonObject jsonObject)
                return jsonObject;

            settings = settings ?? GetDefaultJsonSettings();
            var node = JsonSerializer.SerializeToNode(o, settings);
            return node as JsonObject ?? new JsonObject();
        }

        /// <summary>增加属性（将忽略空值）</summary>
        public static JsonObject AddProperty(this JsonObject jo, string name, object value)
        {
            if (value == null)
                return jo;
            jo[name] = JsonSerializer.SerializeToNode(value);
            return jo;
        }



        //------------------------------------------------
        // 配置
        //------------------------------------------------
        /// <summary>Json 序列化默认配置</summary>
        public static JsonSerializerOptions GetDefaultJsonSettings()
        {
            JsonSerializerOptions settings = new JsonSerializerOptions();
            settings.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;                 // 属性为小写开头驼峰式
            settings.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;      // 忽略null值
            settings.WriteIndented = true;                                               // 递进
            // Keep default MaxDepth (64). A hardcoded small depth (e.g. 5) breaks tree JSON output.
            settings.ReferenceHandler = ReferenceHandler.IgnoreCycles;                   // 指定如何处理循环引用
            settings.Converters.Add(new JsonStringEnumConverter());                      // 枚举输出为字符串
            settings.Converters.Add(new TypeNameJsonConverter());                        // 类型只输出名称和程序集，不输出版本号
            settings.Converters.Add(new DateTimeJsonConverter("yyyy-MM-dd HH:mm:ss"));
            return settings;
        }

        private sealed class DateTimeJsonConverter : JsonConverter<DateTime>
        {
            private readonly string _format;

            public DateTimeJsonConverter(string format)
            {
                _format = string.IsNullOrWhiteSpace(format) ? "yyyy-MM-dd HH:mm:ss" : format;
            }

            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    if (DateTime.TryParse(text, out var value))
                        return value;
                }

                throw new System.Text.Json.JsonException("无效的 DateTime 字符串。");
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(_format));
            }
        }

        private sealed class TypeNameJsonConverter : JsonConverter<Type>
        {
            public override Type Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var name = reader.GetString();
                return Type.GetType(name);
            }

            public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.GetShortName());
            }
        }


        //------------------------------------------------
        // Json 文件
        //------------------------------------------------
        /// <summary>保存 json 到文件</summary>
        public static void SaveJsonFile(this object obj, string filePath, JsonSerializerOptions settings = null)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.Write(obj.ToJson(settings));
                writer.Close();
            }
        }

        /// <summary>读取 Json 文件</summary>
        public static object LoadJsonFile(string filePath, Type type, JsonSerializerOptions settings = null)
        {
            if (!File.Exists(filePath))  return null;
            return File.ReadAllText(filePath).ParseJson(type, settings: settings);
        }

        /// <summary>读取 Json 文件</summary>
        public static T LoadJsonFile<T>(string filePath, JsonSerializerOptions settings = null)
            where T : class
        {
            if (!File.Exists(filePath))  return null;
            return File.ReadAllText(filePath).ParseJson<T>(settings: settings);
        }
    }
}