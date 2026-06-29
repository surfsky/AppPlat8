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

            try
            {
                using var doc = JsonDocument.Parse(dataJson);
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
            }
            catch
            {
                rows.Add(("raw", dataJson));
            }

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
    }
}
