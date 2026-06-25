using System;
using System.Globalization;
using System.Text.RegularExpressions;
using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
    public class MapImageModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string Region { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Att { get; set; }

        public string ImageUrl { get; set; }

        public double? Tlx { get; set; }
        public double? Tly { get; set; }
        public double? Brx { get; set; }
        public double? Bry { get; set; }

        public void OnGet(string region, string selectorValue, string data, string att)
        {
            Att = (att ?? string.Empty).Trim();
            ImageUrl = ResolveImageUrl(Att);

            Region = FirstNonEmpty(region, selectorValue, data)?.Trim();
            if (string.IsNullOrWhiteSpace(Region))
                return;

            var parts = NormalizeParts(Region);
            if (parts.Length < 4)
                return;

            if (!TryParse(parts[0], out var tlx) || !TryParse(parts[1], out var tly)
                || !TryParse(parts[2], out var brx) || !TryParse(parts[3], out var bry))
                return;

            var minLng = Math.Min(tlx, brx);
            var maxLng = Math.Max(tlx, brx);
            var minLat = Math.Min(tly, bry);
            var maxLat = Math.Max(tly, bry);

            if (!double.IsFinite(minLng) || !double.IsFinite(maxLng) || !double.IsFinite(minLat) || !double.IsFinite(maxLat))
                return;
            if (Math.Abs(maxLng - minLng) < 1e-9 || Math.Abs(maxLat - minLat) < 1e-9)
                return;

            Tlx = minLng;
            Tly = maxLat;
            Brx = maxLng;
            Bry = minLat;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null) return string.Empty;
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return string.Empty;
        }

        /// <summary>
        /// 规范化文本，支持以下格式：
        /// 1. 逗号分隔，如"100, 20, 300, 40"
        /// </summary>
        private static string[] NormalizeParts(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<string>();

            var normalized = text
                .Replace('，', ',')
                .Replace('；', ',')
                .Replace(';', ',')
                .Trim();
            normalized = Regex.Replace(normalized, "\\s+", ",");
            return normalized.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static bool TryParse(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        /// <summary>
        /// 解析图片URL，支持以下格式：
        /// 1. 直接的URL，如"http://example.com/image.png"或"/image.png"
        /// 2. 文件查看器URL，如"/Shared/FileViews/Viewer?src=http://example.com/image.png"
        /// </summary>
        private static string ResolveImageUrl(string att)
        {
            if (string.IsNullOrWhiteSpace(att))
                return string.Empty;

            // 移除首尾空格，并按行分割，取第一行
            var text = att.Trim();
            var first = text
                .Replace("\r", ",")
                .Replace("\n", ",")
                .Split(new[] { ',', ';', '，', '；' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (first.Length == 0)
                return string.Empty;
            var raw = first[0].Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            // 文件查看器URL
            if (raw.StartsWith("/Shared/FileViews/Viewer", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var uri = new Uri("http://localhost" + raw, UriKind.Absolute);
                    var query = uri.Query.TrimStart('?');
                    var pairs = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split('=', 2);
                        if (kv.Length != 2)
                            continue;

                        if (!kv[0].Equals("src", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var src = Uri.UnescapeDataString(kv[1] ?? string.Empty).Trim();
                        if (string.IsNullOrWhiteSpace(src))
                            return string.Empty;
                        if (src.StartsWith("~/"))
                            return "/" + src.Substring(2);
                        return src;
                    }
                }
                catch
                {
                    return string.Empty;
                }
            }

            // 直接的URL
            if (raw.StartsWith("~/"))
                return "/" + raw.Substring(2);

            return raw;
        }
    }
}
