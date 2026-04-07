using System.Linq;
using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckSheetEdit)]
    public class CheckSheetItemFormModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long SheetId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SheetName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SheetDisplay { get; set; }

        public CheckSheetItem Item { get; set; }

        public void OnGet(long sheetId, string sheetName)
        {
            SheetId = sheetId;
            SheetName = sheetName;
            if (string.IsNullOrWhiteSpace(SheetName) && SheetId > 0)
            {
                SheetName = CheckSheet.Get(SheetId)?.Name ?? string.Empty;
            }
        }

        private object BuildFormData(CheckSheetItem item, long sheetId, string sheetName)
        {
            var display = string.IsNullOrWhiteSpace(sheetName) ? $"ID:{sheetId}" : $"{sheetName} (ID:{sheetId})";

            return new
            {
                id = item.Id,
                sheetId = sheetId,
                sheetName = sheetName,
                sheetDisplay = display,
                name = item.Name,
                riskLevel = item.RiskLevel,
                sortId = item.SortId
            };
        }

        public IActionResult OnGetData(long id, long sheetId)
        {
            if (sheetId <= 0)
                return BuildResult(400, "参数错误：缺少检查表ID");

            var sheetName = CheckSheet.Get(sheetId)?.Name ?? string.Empty;

            if (id > 0)
            {
                var exists = CheckSheetItem.Set.Any(o => o.Id == id && o.SheetId == sheetId);
                if (!exists)
                    return BuildResult(404, "检查项不存在");

                var item = CheckSheetItem.Get(id);
                return BuildResult(0, "success", BuildFormData(item, sheetId, sheetName));
            }

            return BuildResult(0, "success", BuildFormData(new CheckSheetItem(), sheetId, sheetName));
        }

        public IActionResult OnPostSave([FromBody] CheckSheetItem req, long sheetId)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (sheetId <= 0)
                return BuildResult(400, "参数错误：缺少检查表ID");

            var checkSheet = CheckSheet.Get(sheetId);
            if (checkSheet == null)
                return BuildResult(404, "检查表不存在");

            CheckSheetItem item;
            if (req.Id > 0)
            {
                var exists = CheckSheetItem.Set.Any(o => o.Id == req.Id && o.SheetId == sheetId);
                if (!exists)
                    return BuildResult(403, "无权操作该检查项");

                item = CheckSheetItem.Get(req.Id);
            }
            else
            {
                item = new CheckSheetItem
                {
                    SheetId = sheetId
                };
            }

            item.SheetId = sheetId;
            item.Name = req.Name;
            item.RiskLevel = req.RiskLevel;
            item.SortId = req.SortId;
            item.Save();

            return BuildResult(0, "保存成功");
        }
    }
}
