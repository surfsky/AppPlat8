using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.ArticleEdit)]
    public class ArticleFormModel : AdminModel
    {
        public Article Item { get; set; }
        public List<ArticleDir> Categories { get; set; }

        public void OnGet()
        {
            Categories = ArticleDir.GetTree();
        }



        public IActionResult OnGetData(long id)
        {
            var item = Article.GetDetail(id) ?? new Article();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnGetAttData(Paging pi, long id)
        {
            var article = Article.GetDetail(id);
            var all = BuildAttachmentItems(article?.Attachments);

            var pageIndex = pi?.PageIndex ?? 0;
            var pageSize = (pi?.PageSize ?? 10) > 0 ? pi.PageSize : 10;

            var items = all
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList();

            return BuildResult(0, "success", new
            {
                items,
                total = all.Count
            });
        }

        public IActionResult OnPostSave([FromBody] Article req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = Article.Get(req.Id);
            if (item == null)
            {
                item = new Article();
                item.CreateDt = DateTime.Now;
            }

            item.Name = req.Name;
            item.CategoryId = req.CategoryId;
            item.Content = req.Content;
            item.Attachments = req.Attachments;
            item.AllowComment = req.AllowComment;

            item.Save();
            return BuildResult(0, "保存成功");
        }

        private static List<object> BuildAttachmentItems(string attachments)
        {
            if (string.IsNullOrWhiteSpace(attachments))
                return new List<object>();

            var text = attachments.Trim();

            if (TryParseIdList(text, out var ids) && ids.Count > 0)
            {
                var attMap = Att.Set
                    .Where(t => ids.Contains(t.Id))
                    .ToList()
                    .ToDictionary(t => t.Id, t => t);

                return ids
                    .Where(attMap.ContainsKey)
                    .Select(id =>
                    {
                        var t = attMap[id];
                        return (object)new
                        {
                            id = t.Id,
                            name = string.IsNullOrWhiteSpace(t.FileName) ? Path.GetFileName(t.Url ?? string.Empty) : t.FileName,
                            url = t.Url,
                            sizeText = t.FileSizeText,
                            createDtText = t.CreateDt?.ToString("yyyy-MM-dd")
                        };
                    })
                    .ToList();
            }

            var rows = TryParseAttachmentRows(text);
            if (rows.Count > 0)
                return rows;

            return SplitUrls(text)
                .Select((url, idx) => (object)new
                {
                    id = idx + 1,
                    name = Path.GetFileName(url),
                    url,
                    sizeText = string.Empty,
                    createDtText = string.Empty
                })
                .ToList();
        }

        private static bool TryParseIdList(string text, out List<long> ids)
        {
            ids = new List<long>();

            var parts = text
                .Trim('[', ']')
                .Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().Trim('"', '\''))
                .ToList();

            if (parts.Count == 0)
                return false;

            foreach (var p in parts)
            {
                if (!long.TryParse(p, out var id))
                    return false;
                ids.Add(id);
            }

            return true;
        }

        private static List<object> TryParseAttachmentRows(string text)
        {
            try
            {
                if (!text.StartsWith("["))
                    return new List<object>();

                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    return new List<object>();

                var rows = new List<object>();
                var idx = 1;
                foreach (var token in doc.RootElement.EnumerateArray())
                {
                    if (token.ValueKind == JsonValueKind.String)
                    {
                        var url = token.ToString();
                        if (string.IsNullOrWhiteSpace(url))
                            continue;

                        rows.Add(new
                        {
                            id = idx++,
                            name = Path.GetFileName(url),
                            url,
                            sizeText = string.Empty,
                            createDtText = string.Empty
                        });
                        continue;
                    }

                    if (token.ValueKind != JsonValueKind.Object)
                        continue;

                    var urlValue = GetString(token, "url", "content", "path", "value");
                    if (string.IsNullOrWhiteSpace(urlValue))
                        continue;

                    rows.Add(new
                    {
                        id = GetInt64(token, "id") ?? idx,
                        name = GetString(token, "name", "fileName") ?? Path.GetFileName(urlValue),
                        url = urlValue,
                        sizeText = GetString(token, "sizeText", "fileSizeText") ?? string.Empty,
                        createDtText = GetString(token, "createDtText", "createDt") ?? string.Empty
                    });
                    idx++;
                }

                return rows;
            }
            catch
            {
                return new List<object>();
            }
        }

        private static string GetString(JsonElement obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (obj.TryGetProperty(key, out var value))
                {
                    if (value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Undefined)
                        continue;
                    return value.ToString();
                }
            }

            return null;
        }

        private static long? GetInt64(JsonElement obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!obj.TryGetProperty(key, out var value))
                    continue;

                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n))
                    return n;

                if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out n))
                    return n;
            }

            return null;
        }

        private static List<string> SplitUrls(string text)
        {
            return text
                .Split(new[] { '\n', '\r', ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim().Trim('"', '\''))
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
        }
    }
}
