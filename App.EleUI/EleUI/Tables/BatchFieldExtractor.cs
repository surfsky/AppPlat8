using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using App.Utils;

namespace App.EleUI
{
    internal static class BatchFieldExtractor
    {
        /// <summary>
        /// 解析 EleButton 子内容中所有 BatchField 元素，输出元数据列表。
        /// 解析方式：把子内容 HTML 按 <BatchField ... /> 或 <BatchField>...</BatchField> 形式用正则/XML DOM 抽取属性。
        /// 支持：
        ///   <BatchField Control="Picker" Field="CheckerId" Label="检查员" PopupUrl="/Shared/UserSelector?multi=0" TextFor="CheckerName" />
        ///   <BatchField Control="Input"  Field="DutyMan"   Label="企业负责人" Placeholder="留空则不更新" Required="false" />
        /// </summary>
        public static List<Dictionary<string, object>> Extract(string childHtmlRaw)
        {
            var list = new List<Dictionary<string, object>>();
            if (string.IsNullOrWhiteSpace(childHtmlRaw))
                return list;

            var matches = Regex.Matches(
                childHtmlRaw,
                @"<BatchField\s+(?<attrs>.*?)\s*/?>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                var attrsRaw = m.Groups["attrs"].Value;
                var attrs = ParseHtmlAttributes(attrsRaw);
                var field = new BatchFieldTagHelper();

                // C# 属性 ↔ HTML 属性大小写不敏感映射
                var map = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Control"]     = v => { if (Enum.TryParse(v, true, out EleBatchFieldControl c)) field.Control = c; },
                    ["Field"]       = v => field.Field       = v,
                    ["Label"]       = v => field.Label       = v,
                    ["Placeholder"] = v => field.Placeholder = v,
                    ["Required"]    = v => field.Required    = bool.TryParse(v, out var b) && b,
                    ["Enabled"]     = v => field.Enabled     = !bool.TryParse(v, out var b) || b,
                    ["PopupUrl"]    = v => field.PopupUrl    = v,
                    ["TextFor"]     = v => field.TextFor     = v,
                    ["Multi"]       = v => field.Multi       = bool.TryParse(v, out var b) && b,
                    ["Options"]     = v => field.Options     = v,
                    ["Min"]         = v => { if (double.TryParse(v, out var d)) field.Min = d; },
                    ["Max"]         = v => { if (double.TryParse(v, out var d)) field.Max = d; },
                    ["Step"]        = v => { if (double.TryParse(v, out var d)) field.Step = d; },
                    ["Rows"]        = v => { if (int.TryParse(v, out var i))    field.Rows = i; },
                };
                foreach (var a in attrs)
                {
                    if (map.TryGetValue(a.Key, out var setter))
                        setter(a.Value);
                }
                list.Add(field.ToMeta());
            }
            return list;
        }

        /// <summary>调试入口：返回所有 BatchField 的原始属性串及解析结果</summary>
        public static List<Dictionary<string, object>> ParseHtmlAttributesDebug(string childHtmlRaw)
        {
            var list = new List<Dictionary<string, object>>();
            if (string.IsNullOrWhiteSpace(childHtmlRaw))
                return list;
            var matches = Regex.Matches(
                childHtmlRaw,
                @"<BatchField\s+(?<attrs>.*?)\s*/?>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var attrsRaw = m.Groups["attrs"].Value;
                var parsed = ParseHtmlAttributes(attrsRaw);
                list.Add(new Dictionary<string, object>
                {
                    ["rawAttrs"] = attrsRaw.Length > 500 ? attrsRaw.Substring(0, 500) : attrsRaw,
                    ["parsedAttrs"] = new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase),
                    ["rawAttrsLength"] = attrsRaw.Length,
                    ["hasQuote"] = attrsRaw.Contains("\"") || attrsRaw.Contains("&quot;"),
                });
            }
            return list;
        }

        /// <summary>解析HTML属性串：name="val" | name='val' | name (无值布尔属性)
        /// 兼容：属性串中引号可能被编码为 &quot; 或被 Razor/JSON 转义为 \"，解析前统一还原为真实双引号。
        /// 兼容：PopupUrl 等属性值可能含 ?=& 等字符（如 /Shared/UserSelector?multi=0），只要在引号内即可正确捕获。</summary>
        internal static Dictionary<string, string> ParseHtmlAttributes(string raw)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw))
                return dict;

            var normalized = raw;
            normalized = normalized.Replace("&quot;", "\"");
            normalized = normalized.Replace("&#34;", "\"");
            normalized = normalized.Replace("&#x22;", "\"");
            normalized = System.Net.WebUtility.HtmlDecode(normalized);
            normalized = Regex.Replace(normalized, @"\\+""", "\"");

            var matches = Regex.Matches(
                normalized,
                @"(?<name>[A-Za-z_][A-Za-z0-9_\-]*)(?:\s*=\s*(?:""(?<v1>[^""]*)""|'(?<v2>[^']*)'|(?<v3>[^\s""'=<>`]+)))?",
                RegexOptions.Singleline);
            foreach (Match m in matches)
            {
                var name = m.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(name)) continue;
                var v = m.Groups["v1"].Success ? m.Groups["v1"].Value
                      : m.Groups["v2"].Success ? m.Groups["v2"].Value
                      : m.Groups["v3"].Success ? m.Groups["v3"].Value
                      : "true";
                dict[name] = v;
            }
            return dict;
        }
    }
}
