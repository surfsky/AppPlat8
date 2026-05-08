using System;
using System.Collections.Generic;
using System.Configuration;
using System.Reflection;
using System.Web;
//using AppPlat.HttpApi.Properties;
using HttpApi.Properties;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.HttpApi
{
        public enum Formatting
        {
            None = 0,
            Indented = 1,
        }

        public class VisitArgs
        {
            public HttpContext Context { get; set; }
            public MethodInfo Method { get; set; }
            public HttpApiAttribute Attr { get; set; }
            public Dictionary<string, object> Inputs { get; set; }
        }

        public class AuthArgs
        {
            public HttpContext Context { get; set; }
            public MethodInfo Method { get; set; }
            public HttpApiAttribute Attr { get; set; }
            public string Token { get; set; }
        }

        public class EndArgs
        {
            public HttpContext Context { get; set; }
        }

        public class ExceptionArgs
        {
            public HttpContext Context { get; set; }
            public MethodInfo Method { get; set; }
            public Exception Ex { get; set; }
        }

        public class BanArgs
        {
            public HttpContext Context { get; set; }
            public string IP { get; set; }
            public string Url { get; set; }
        }

    /*
    "httpApi" : {
        authIPs = "",
        errorResponse="DataResult",
        jsonEnumFormatting="Text",
        wrap="",
        jsonIndented="Indented",
        jsonDateTimeFormat="yyyy-MM-dd"
    }
    */
    /// <summary>
    /// HttpApi 配置
    /// </summary>
    public class HttpApiConfig
    {
        public bool FormatLowCamel          { get; set; } = true;
        public Formatting FormatIndented    { get; set; } = Formatting.Indented;
        public EnumFomatting FormatEnum     { get; set; } = EnumFomatting.Text;
        public string FormatDateTime        { get; set; } = "yyyy-MM-dd HH:mm:ss";
        public string FormatLongNumber      { get; set; } = "Int64,UInt64,Decimal";
        public ErrorResponse ErrorResponse  { get; set; } = ErrorResponse.APIResult;
        public string TypePrefix            { get; set; } = "AppPlat.API.";
        public string Language              { get; set; } = "en";
        public bool? Wrap                   { get; set; } = null;
        public int? MaxDepth                { get; set; } = null;
        public int? BanMinutes              { get; set; } = null;

        /// <summary>System.Text.Json Serializer Options</summary>
        public JsonSerializerOptions JsonOptions { get; set; }


        //public static void Configure(IHttpContextAccessor contextAccessor)
        //{
        //    Asp.Configure(contextAccessor);
        //}

        //--------------------------------------------------
        // 单例
        //--------------------------------------------------
        private static HttpApiConfig _instance = null;
        public static HttpApiConfig Instance 
        {
            get 
            {
                if (_instance == null)
                {
                    // 尝试从配置节中恢复配置。若未找到配置节，则赋予默认值。
                    //var cfg = Configuration.GetSection<HttpApiConfig>("httpApi");
                    _instance = new HttpApiConfig();
                    _instance.JsonOptions = _instance.GetJsonOptions();

                    // 设置国际化支持
                    Resources.Culture = new System.Globalization.CultureInfo(_instance.Language);
                }
                return _instance;
            }
        }

        /// <summary>从配置中获取 System.Text.Json 序列化选项</summary>
        public JsonSerializerOptions GetJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = this.FormatLowCamel ? JsonNamingPolicy.CamelCase : null,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles,
                WriteIndented = this.FormatIndented == Formatting.Indented
            };

            if (this.MaxDepth.HasValue && this.MaxDepth.Value > 0)
                options.MaxDepth = this.MaxDepth.Value;

            options.Converters.Add(new FormattedDateTimeJsonConverter(this.FormatDateTime));
            options.Converters.Add(new NullableFormattedDateTimeJsonConverter(this.FormatDateTime));

            if (this.FormatEnum == EnumFomatting.Text)
                options.Converters.Add(new JsonStringEnumConverter());

            var types = this.FormatLongNumber.ParseEnums<TypeCode>();
            if (types.Contains(TypeCode.Int64))
            {
                options.Converters.Add(new Int64ToStringJsonConverter());
                options.Converters.Add(new NullableInt64ToStringJsonConverter());
            }

            if (types.Contains(TypeCode.UInt64))
            {
                options.Converters.Add(new UInt64ToStringJsonConverter());
                options.Converters.Add(new NullableUInt64ToStringJsonConverter());
            }

            if (types.Contains(TypeCode.Decimal))
            {
                options.Converters.Add(new DecimalToStringJsonConverter());
                options.Converters.Add(new NullableDecimalToStringJsonConverter());
            }

            return options;
        }

        private sealed class FormattedDateTimeJsonConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
        {
            private readonly string _format;

            public FormattedDateTimeJsonConverter(string format)
            {
                _format = string.IsNullOrWhiteSpace(format) ? "yyyy-MM-dd HH:mm:ss" : format;
            }

            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    if (DateTime.TryParseExact(text, _format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        return dt;
                    if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        return dt;
                    throw new System.Text.Json.JsonException($"Invalid DateTime value: {text}");
                }

                throw new System.Text.Json.JsonException($"Unexpected token parsing DateTime: {reader.TokenType}");
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(_format, CultureInfo.InvariantCulture));
            }
        }

        private sealed class NullableFormattedDateTimeJsonConverter : System.Text.Json.Serialization.JsonConverter<DateTime?>
        {
            private readonly string _format;

            public NullableFormattedDateTimeJsonConverter(string format)
            {
                _format = string.IsNullOrWhiteSpace(format) ? "yyyy-MM-dd HH:mm:ss" : format;
            }

            public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                    return null;

                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    if (string.IsNullOrWhiteSpace(text))
                        return null;
                    if (DateTime.TryParseExact(text, _format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        return dt;
                    if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                        return dt;
                    throw new System.Text.Json.JsonException($"Invalid nullable DateTime value: {text}");
                }

                throw new System.Text.Json.JsonException($"Unexpected token parsing nullable DateTime: {reader.TokenType}");
            }

            public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
            {
                if (!value.HasValue)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStringValue(value.Value.ToString(_format, CultureInfo.InvariantCulture));
            }
        }

        private sealed class Int64ToStringJsonConverter : System.Text.Json.Serialization.JsonConverter<long>
        {
            public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        return value;
                    throw new System.Text.Json.JsonException($"Invalid Int64 string value: {text}");
                }

                if (reader.TokenType == JsonTokenType.Number)
                    return reader.GetInt64();

                throw new System.Text.Json.JsonException($"Unexpected token parsing Int64: {reader.TokenType}");
            }

            public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private sealed class NullableInt64ToStringJsonConverter : System.Text.Json.Serialization.JsonConverter<long?>
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
                    throw new System.Text.Json.JsonException($"Invalid nullable Int64 string value: {text}");
                }

                if (reader.TokenType == JsonTokenType.Number)
                    return reader.GetInt64();

                throw new System.Text.Json.JsonException($"Unexpected token parsing nullable Int64: {reader.TokenType}");
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

        private sealed class UInt64ToStringJsonConverter : System.Text.Json.Serialization.JsonConverter<ulong>
        {
            public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    if (ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                        return value;
                    throw new System.Text.Json.JsonException($"Invalid UInt64 string value: {text}");
                }

                if (reader.TokenType == JsonTokenType.Number)
                    return reader.GetUInt64();

                throw new System.Text.Json.JsonException($"Unexpected token parsing UInt64: {reader.TokenType}");
            }

            public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private sealed class NullableUInt64ToStringJsonConverter : System.Text.Json.Serialization.JsonConverter<ulong?>
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
                    throw new System.Text.Json.JsonException($"Invalid nullable UInt64 string value: {text}");
                }

                if (reader.TokenType == JsonTokenType.Number)
                    return reader.GetUInt64();

                throw new System.Text.Json.JsonException($"Unexpected token parsing nullable UInt64: {reader.TokenType}");
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

        private sealed class DecimalToStringJsonConverter : System.Text.Json.Serialization.JsonConverter<decimal>
        {
            public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                        return value;
                    throw new System.Text.Json.JsonException($"Invalid Decimal string value: {text}");
                }

                if (reader.TokenType == JsonTokenType.Number)
                    return reader.GetDecimal();

                throw new System.Text.Json.JsonException($"Unexpected token parsing Decimal: {reader.TokenType}");
            }

            public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private sealed class NullableDecimalToStringJsonConverter : System.Text.Json.Serialization.JsonConverter<decimal?>
        {
            public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                    return null;

                if (reader.TokenType == JsonTokenType.String)
                {
                    var text = reader.GetString();
                    if (string.IsNullOrWhiteSpace(text))
                        return null;
                    if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                        return value;
                    throw new System.Text.Json.JsonException($"Invalid nullable Decimal string value: {text}");
                }

                if (reader.TokenType == JsonTokenType.Number)
                    return reader.GetDecimal();

                throw new System.Text.Json.JsonException($"Unexpected token parsing nullable Decimal: {reader.TokenType}");
            }

            public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
            {
                if (!value.HasValue)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStringValue(value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }


        //--------------------------------------------------
        // HttpApi访问事件，请在Global中设置
        //--------------------------------------------------

        public delegate void VisitHandler(VisitArgs args);
        public delegate void AuthHandler(AuthArgs args);
        public delegate void EndHandler(EndArgs args);
        public delegate void ExceptionHandler(ExceptionArgs args);
        public delegate void BanHandler(BanArgs args);

        /// <summary>访问事件（有异常请直接抛出 HttpApiException 异常）</summary>
        public event VisitHandler OnVisit;

        /// <summary>鉴权事件（有异常请直接抛出 HttpApiException 异常）</summary>
        public event AuthHandler OnAuth;

        /// <summary>结束事件（有异常请直接抛出 HttpApiException 异常）</summary>
        public event EndHandler OnEnd;

        /// <summary>异常时间</summary>
        public event ExceptionHandler OnException;

        /// <summary>禁止事件</summary>
        public event BanHandler OnBan;


        //--------------------------------------------
        // 包裹方法
        //--------------------------------------------
        public void DoVisit(HttpContext context, MethodInfo method, HttpApiAttribute attr, Dictionary<string, object> inputs)
        {
            DoVisit(new VisitArgs { Context = context, Method = method, Attr = attr, Inputs = inputs });
        }
        public void DoVisit(VisitArgs args)
        {
            this.OnVisit?.Invoke(args);
        }



        public void DoAuth(HttpContext context, MethodInfo method, HttpApiAttribute attr, string token)
        {
            DoAuth(new AuthArgs { Context = context, Method = method, Attr = attr, Token = token });
        }
        /// <summary>授权事件</summary>
        public void DoAuth(AuthArgs args)
        {
            this.OnAuth?.Invoke(args);
        }


        public void DoEnd(HttpContext context)
        {
            DoEnd(new EndArgs { Context = context });
        }
        /// <summary>结束</summary>
        public void DoEnd(EndArgs args)
        {
            this.OnEnd?.Invoke(args);
        }


        public void DoException(HttpContext context, MethodInfo method, Exception ex)
        {
            DoException(new ExceptionArgs { Context = context, Method = method, Ex = ex });
        }
        /// <summary>异常处理</summary>
        public void DoException(ExceptionArgs args)
        {
            this.OnException?.Invoke(args);
        }


        public void DoBan(HttpContext context, string ip, string url)
        {
            DoBan(new BanArgs { Context = context, IP = ip, Url = url });
        }
        /// <summary>禁止访问</summary>
        public void DoBan(BanArgs args)
        {
            this.OnBan?.Invoke(args);
        }
    }
}
