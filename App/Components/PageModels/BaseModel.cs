using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Dynamic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using App.DAL;
using App.Components;
using App.HttpApi;

namespace App
{
    /// <summary>
    /// 页面模型基类，提供一些公共方法和属性
    /// </summary>
    public partial class BaseModel : PageModel
    {
        private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                ReferenceHandler = ReferenceHandler.IgnoreCycles
            };

            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new Int64ToStringJsonConverter());
            options.Converters.Add(new NullableInt64ToStringJsonConverter());
            options.Converters.Add(new UInt64ToStringJsonConverter());
            options.Converters.Add(new NullableUInt64ToStringJsonConverter());
            return options;
        }

        private sealed class Int64ToStringJsonConverter : JsonConverter<long>
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

        private sealed class NullableInt64ToStringJsonConverter : JsonConverter<long?>
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

        private sealed class UInt64ToStringJsonConverter : JsonConverter<ulong>
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

        private sealed class NullableUInt64ToStringJsonConverter : JsonConverter<ulong?>
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
        /// 导出当前模型上标记了 [BindProperty] 的属性，供前端初始化/回传使用。
        /// </summary>
        public object Export()
        {
            IDictionary<string, object> data = new ExpandoObject();
            var properties = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);

            foreach (var property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;
                if (property.GetCustomAttribute<BindPropertyAttribute>() == null)
                    continue;

                data[property.Name] = property.GetValue(this);
            }

            return data;
        }

        //-------------------------------------------------
        // 构建API结果
        //-------------------------------------------------
        /// <summary>构建API结果</summary>
        public static JsonResult BuildResult(int code, string msg, object data = null, Paging pager = null)
        {
            return new JsonResult(new APIResult(code, msg, data, pager), _jsonOptions);
        }



        //-------------------------------------------------
        // 页面处理事件
        //-------------------------------------------------
        /// <summary>页面处理器调用之前执行（加了在线逻辑）</summary>
        public override void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            base.OnPageHandlerExecuting(context);

            // 如果用户已经登录，更新在线记录
            if (User.Identity.IsAuthenticated)
                Auth.UpdateOnlineUser(GetUserId());
        }

        public override void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            base.OnPageHandlerExecuted(context);
        }



        //-------------------------------------------------
        // 用户权限和校验
        //-------------------------------------------------
        /// <summary>检查当前用户是否拥有某个权限</summary>
        protected bool CheckPower(Power power) => Auth.CheckPower(HttpContext, power);

        /// <summary>获取当前登录用户拥有的全部权限列表</summary>
        protected List<Power> GetPowers() => Auth.GetUserPowers(HttpContext);

        /// <summary>当前登录用户名</summary>
        protected string GetUserName() => Auth.GetUserName(HttpContext);

        /// <summary>当前登录用户标识符</summary>
        protected long? GetUserId() => Auth.GetUserId(HttpContext);

        protected App.DAL.User GetUser() => Auth.GetUser();
    }
}
