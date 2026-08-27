//using System.Web.Script.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace App.HttpApi
{
    /// <summary>请求解码器基类</summary>
    public abstract class RequestDecoder
    {
        protected HttpContext _context;
        protected RequestDecoder(HttpContext context)
        {
            this._context = context;
        }

        /// <summary>创建解码器（尝试根据ContentType来构造解析器，但往往不准确，客户端没那么乖）</summary>
        public static RequestDecoder CreateInstance(HttpContext context)
        {
            var method = (context.Request.Method ?? string.Empty).ToUpperInvariant();
            bool hasBody;
            try
            {
                var cl = context.Request.ContentLength;
                hasBody = (cl.HasValue && cl.Value > 0)
                          || (!cl.HasValue && "POST".Equals(method, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                hasBody = "POST".Equals(method, StringComparison.OrdinalIgnoreCase);
            }

            string contentType = context.Request.ContentType?.ToLower();
            if (!string.IsNullOrEmpty(contentType))
            {
                if (contentType.IndexOf("application/json") >= 0)
                    return new JsonDecoder(context);
                if (contentType.IndexOf("application/x-www-form-urlencoded") >= 0)
                    return new JsonDecoder(context);
                if (contentType.IndexOf("multipart/") >= 0)
                    return new MultipartFormDecoder(context);
                if (contentType.IndexOf("application/xml") >= 0)
                    return new JsonDecoder(context);
            }

            if ("POST".Equals(method, StringComparison.OrdinalIgnoreCase) && hasBody)
                return new JsonDecoder(context);

            if (context.Request.Query.Count > 0)
                return new UrlDecoder(context);

            return new UrlDecoder(context);
        }

        /// <summary>取得方法名（以url最后一部分作为方法名。如：..\Handler1.ashx\GetData）</summary>
        public virtual string MethodName
        {
            get
            {
                //int n = this._context.Request.Url.Segments.Length;
                //return this._context.Request.Url.Segments[n - 1];
                //int n = this._context.Request.Path..Segments.Length;
                //return this._context.Request.Url.Segments[n - 1];
                var u = new Url(_context.Request.Path.ToString());
                return u.FileName;
            }
        }

        /// <summary>解析请求参数</summary>
        public abstract Dictionary<string, object> ParseArguments();
    }




    ///-----------------------------------------------
    /// URL
    ///-----------------------------------------------
    /// <summary>URL 解码器</summary>
    internal class UrlDecoder : RequestDecoder
    {
        internal UrlDecoder(HttpContext context)
            : base(context)
        {
        }

        public override Dictionary<string, object> ParseArguments()
        {
            var data = new Dictionary<string, object>();
            var qs = this._context.Request.QueryString.Value;
            var dict = qs.ParseDict();
            foreach (var key in  dict.Keys)
                data.Add(key, dict[key]);

            return data;
        }

        public override string MethodName
        {
            get
            {

                //int n = this._context.Request.Url.Segments.Length;
                //string methodName = this._context.Request.Url.Segments[n - 1];
                //if (methodName.ToLower().LastIndexOf(".ashx") >= 0)
                //    return "js";  // 缺省函数名称
                //else
                //    return methodName;

                var u = new Url(_context.Request.Path.ToString());
                return (u.FileExtesion == ".ashx") ? "js" : u.FileName;
            }
        }
    }

    ///-----------------------------------------------
    /// JSON POST
    ///-----------------------------------------------
    /// <summary>JSON 解码器</summary>
    internal class JsonDecoder : RequestDecoder
    {
        internal JsonDecoder(HttpContext context)
            : base(context)
        {
        }

        /// <summary>解析参数</summary>
        public override Dictionary<string, object> ParseArguments()
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // 仅当确实存在 form 内容时才读 Form，避免对 无 body 的 POST 或纯 JSON POST 抛异常
            try
            {
                var canReadForm = false;
                try
                {
                    var cl = _context.Request.ContentLength;
                    var ct = _context.Request.ContentType?.ToLower() ?? string.Empty;
                    canReadForm = (cl.HasValue && cl.Value > 0)
                                  && (ct.IndexOf("application/x-www-form-urlencoded", StringComparison.Ordinal) >= 0
                                      || ct.IndexOf("multipart/", StringComparison.Ordinal) >= 0);
                }
                catch { canReadForm = false; }

                if (canReadForm)
                {
                    foreach (var item in _context.Request.Form)
                        dict[item.Key] = item.Value;
                }
            }
            catch (InvalidOperationException) { /* 缺 Content-Type 或缺 Body 时忽略 */ }
            catch (System.IO.IOException) { }

            // 附加上 QueryString
            foreach (var q in _context.Request.Query)
                dict[q.Key] = q.Value;

            // 尝试读 JSON Body
            try
            {
                var ct = _context.Request.ContentType?.ToLower() ?? string.Empty;
                var cl = _context.Request.ContentLength;
                var bodyPresent = (cl.HasValue && cl.Value > 0);
                if (ct.IndexOf("application/json", StringComparison.Ordinal) >= 0
                    && bodyPresent
                    && _context.Request.Body != null && _context.Request.Body.CanRead)
                {
                    using var ms = new System.IO.MemoryStream();
                    var copyTask = _context.Request.Body.CopyToAsync(ms);
                    copyTask.ConfigureAwait(false).GetAwaiter().GetResult();
                    var body = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(body);
                            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                            {
                                foreach (var prop in doc.RootElement.EnumerateObject())
                                    dict[prop.Name] = JsonElementToValue(prop.Value);
                            }
                            dict["$body"] = body;
                        }
                        catch { }
                    }
                }
            }
            catch (NotSupportedException) { /* HttpRequestStream.get_Length 不支持 */ }
            catch (System.IO.IOException) { }
            catch { }

            return dict;
        }

        static object JsonElementToValue(System.Text.Json.JsonElement el)
        {
            switch (el.ValueKind)
            {
                case System.Text.Json.JsonValueKind.String:
                    return el.GetString();
                case System.Text.Json.JsonValueKind.Number:
                    if (el.TryGetInt64(out var l)) return l;
                    if (el.TryGetDouble(out var d)) return d;
                    return el.GetRawText();
                case System.Text.Json.JsonValueKind.True: return true;
                case System.Text.Json.JsonValueKind.False: return false;
                case System.Text.Json.JsonValueKind.Null: return null;
                default: return el.GetRawText();
            }
        }

        private Encoding GetRequestEncoding(HttpRequest request)
        {
            var requestContentType = request.ContentType;
            var requestMediaType = requestContentType == null ? default(MediaType) : new MediaType(requestContentType);
            var requestEncoding = requestMediaType.Encoding;
            if (requestEncoding == null)
                requestEncoding = Encoding.UTF8;

            return requestEncoding;
        }

    }


    ///-----------------------------------------------
    /// <summary> Multipart Form 解码器（带附件）</summary>
    internal class MultipartFormDecoder : RequestDecoder
    {
        internal MultipartFormDecoder(HttpContext context)
            : base(context)
        {
        }

        /// <summary>解析参数</summary>
        public override Dictionary<string, object> ParseArguments()
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var cl = _context.Request.ContentLength;
                if (!cl.HasValue || cl.Value > 0)
                {
                    foreach (var item in _context.Request.Form)
                        dict[item.Key] = item.Value;
                }
            }
            catch (InvalidOperationException) { }
            catch (System.IO.IOException) { }

            foreach (var item in _context.Request.Query)
                dict[item.Key] = item.Value;
            return dict;
        }
    }
}
