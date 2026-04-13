using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckSheetView)]
    public class CheckSheetsModel : AdminModel
    {
        public CheckSheet Item { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnGetData(Paging pi, string name, CheckScope? scope)
        {
            var q = CheckSheet.Search(name, scope);
            pi.SetTotal(q.Count());

            var sheets = q.SortAndPage(pi).ToList();
            var sheetIds = sheets.Select(s => s.Id).ToList();

            var tagTextMap = new Dictionary<long, string>();
            var itemCountMap = new Dictionary<long, int>();

            if (sheetIds.Count > 0)
            {
                var tags = CheckTag.Set
                    .Include(t => t.Sheets)
                    .Where(t => t.Sheets.Any(s => sheetIds.Contains(s.Id)))
                    .ToList();

                var tagPairs = tags
                    .SelectMany(t => t.Sheets
                        .Where(s => sheetIds.Contains(s.Id))
                        .Select(s => new { SheetId = s.Id, TagName = t.Name }))
                    .ToList();

                tagTextMap = tagPairs
                    .GroupBy(t => t.SheetId)
                    .ToDictionary(
                        g => g.Key,
                        g => string.Join("，", g.Select(x => x.TagName).Distinct())
                    );

                itemCountMap = CheckSheetItem.Set
                    .Where(i => sheetIds.Contains(i.SheetId))
                    .GroupBy(i => i.SheetId)
                    .Select(g => new { SheetId = g.Key, Count = g.Count() })
                    .ToDictionary(x => x.SheetId, x => x.Count);
            }

            var list = sheets
                .Select(s =>
                {
                    tagTextMap.TryGetValue(s.Id, out var tagNames);
                    itemCountMap.TryGetValue(s.Id, out var itemCount);
                    return new
                    {
                        s.Id,
                        s.Name,
                        s.Scope,
                        TagNames = tagNames ?? string.Empty,
                        ItemCount = itemCount,
                        s.CreateDt
                    };
                })
                .Cast<object>()
                .ToList();

            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckSheetDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                CheckSheet.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
