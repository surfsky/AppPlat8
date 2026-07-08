using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryView)]
    public class GeometryInfoModel : AdminModel
    {
        public GisGeometry Item { get; set; } = new GisGeometry();
        public List<(string Key, string Value)> DataRows { get; set; } = new List<(string Key, string Value)>();

        public IActionResult OnGet(long id)
        {
            if (id <= 0)
                return Content("参数错误");

            var item = GisGeometry.DataSet
                .Include(g => g.Menu)
                .FirstOrDefault(g => g.Id == id);

            if (item == null)
                return Content("点位不存在或无权访问");

            Item = item;
            DataRows = ParseDataRows(item.DataJson);
            return Page();
        }

        private static List<(string Key, string Value)> ParseDataRows(string dataJson)
        {
            var rows = new List<(string Key, string Value)>();
            if (string.IsNullOrWhiteSpace(dataJson))
                return rows;

            foreach (var candidate in EnumerateJsonCandidates(dataJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(candidate);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in root.EnumerateObject())
                            rows.Add((p.Name, FormatToken(p.Value)));
                    }
                    else if (root.ValueKind == JsonValueKind.Array)
                    {
                        var i = 0;
                        foreach (var item in root.EnumerateArray())
                        {
                            rows.Add(($"[{i}]", FormatToken(item)));
                            i++;
                        }
                    }
                    else
                    {
                        rows.Add(("value", FormatToken(root)));
                    }

                    if (rows.Count > 0)
                        return rows;
                }
                catch
                {
                    // ignore and continue trying normalized candidates
                }
            }

            rows.Add(("原文", dataJson.Trim()));
            return rows;
        }

        private static string FormatToken(JsonElement token)
        {
            string text;
            if (token.ValueKind == JsonValueKind.String)
                text = token.GetString() ?? "";
            else if (token.ValueKind == JsonValueKind.Null || token.ValueKind == JsonValueKind.Undefined)
                text = "";
            else
                text = JsonSerializer.Serialize(token);

            return NormalizeQuotedText(text);
        }

        private static string NormalizeQuotedText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? "";

            var text = input.Trim();

            // Handle values like "text" or \"text\" and keep plain text output.
            for (var i = 0; i < 3; i++)
            {
                if ((text.StartsWith("\"") && text.EndsWith("\"")) || (text.StartsWith("'") && text.EndsWith("'")))
                    text = text.Substring(1, text.Length - 2).Trim();

                if (text.StartsWith("\\\"") && text.EndsWith("\\\"") && text.Length >= 4)
                    text = text.Substring(2, text.Length - 4).Trim();
                else
                    break;
            }

            return text;
        }

        private static IEnumerable<string> EnumerateJsonCandidates(string dataJson)
        {
            var seen = new HashSet<string>();
            var text = (dataJson ?? string.Empty).Trim();
            for (var i = 0; i < 4 && !string.IsNullOrWhiteSpace(text); i++)
            {
                if (seen.Add(text))
                    yield return text;

                var normalized = NormalizeJsonLikeText(text);
                if (!string.IsNullOrWhiteSpace(normalized) && seen.Add(normalized))
                    yield return normalized;

                var next = TryUnwrapJsonText(text);
                if (string.IsNullOrWhiteSpace(next) || string.Equals(next, text))
                    yield break;

                text = next;
            }
        }

        private static string TryUnwrapJsonText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text ?? "";

            var normalized = NormalizeJsonLikeText(text);
            try
            {
                if ((normalized.StartsWith("\"") && normalized.EndsWith("\""))
                    || (normalized.StartsWith("'") && normalized.EndsWith("'")))
                {
                    var decoded = JsonSerializer.Deserialize<string>(normalized.Replace('\'', '"'));
                    if (!string.IsNullOrWhiteSpace(decoded))
                        return decoded.Trim();
                }
            }
            catch
            {
                // ignore and fallback to manual unescape
            }

            var manual = NormalizeQuotedText(normalized);
            if (!string.Equals(manual, normalized))
                return manual;

            return normalized.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();
        }

        private static string NormalizeJsonLikeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text ?? "";

            return text.Trim()
                .Replace('“', '"')
                .Replace('”', '"')
                .Replace('‘', '\'')
                .Replace('’', '\'');
        }
    }
}
