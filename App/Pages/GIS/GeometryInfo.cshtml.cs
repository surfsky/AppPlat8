using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
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
                var token = JToken.Parse(dataJson);
                if (token is JObject obj)
                {
                    foreach (var p in obj.Properties())
                        rows.Add((p.Name, FormatToken(p.Value)));
                }
                else if (token is JArray arr)
                {
                    for (var i = 0; i < arr.Count; i++)
                        rows.Add(($"[{i}]", FormatToken(arr[i])));
                }
                else
                {
                    rows.Add(("value", FormatToken(token)));
                }
            }
            catch
            {
                rows.Add(("raw", dataJson));
            }

            return rows;
        }

        private static string FormatToken(JToken token)
        {
            if (token == null)
                return "";

            string text;
            if (token.Type == JTokenType.String)
                text = token.Value<string>() ?? "";
            else
                text = token.ToString(Newtonsoft.Json.Formatting.None);

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
