using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckSheetView)]
    public class CheckSheetItemsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long SheetId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SheetName { get; set; }

        public CheckSheetItem Item { get; set; }

        public void OnGet(long sheetId)
        {
            SheetId = sheetId;
            if (string.IsNullOrWhiteSpace(SheetName))
            {
                SheetName = CheckSheet.Get(sheetId)?.Name ?? string.Empty;
            }
        }

        public IActionResult OnGetData(Paging pi, long sheetId, CheckRiskLevel? riskLevel, string name)
        {
            if (sheetId <= 0)
                return BuildResult(400, "参数错误：缺少检查表ID");

            var list = CheckSheetItem.Search(sheetId, riskLevel, name).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids, long sheetId)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (sheetId <= 0)
                return BuildResult(400, "参数错误：缺少检查表ID");
            if (!CheckPower(Power.CheckSheetEdit))
                return BuildResult(403, "无权操作");

            var items = CheckSheetItem.Set
                .Where(o => ids.Contains(o.Id) && o.SheetId == sheetId)
                .ToList();

            foreach (var item in items)
                item.Delete();

            return BuildResult(0, "删除成功");
        }
    }
}
