using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.EleUI;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [Auth(Power.CheckSheetView)]
    public class CheckSheetFormModel : AdminModel
    {
        public CheckSheet Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id)
        {
            var item = CheckSheet.GetDetail(id) ?? new CheckSheet();
            return BuildResult(0, "success", item.Export(ExportMode.Normal));
        }

        public IActionResult OnPostSave([FromBody] CheckSheet req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            CheckSheet item;
            if (req.Id > 0)
            {
                item = CheckSheet.Set.Include(o => o.Tags).FirstOrDefault(o => o.Id == req.Id);
                if (item == null)
                    return BuildResult(404, "检查表不存在");
            }
            else
            {
                item = new CheckSheet();
                item.CreateDt = DateTime.Now;
            }

            item.Name = req.Name;
            item.SortId = req.SortId;
            item.Scope = req.Scope;
            item.SetTags(req.TagIds);
            item.Save();
            return BuildResult(0, "保存成功", item.Export(ExportMode.Normal));
        }


        /// <summary>EleList 数据接口：按 SortId 升序返回检查项（分页）</summary>
        public IActionResult OnGetItemsData(Paging pi, long sheetId, CheckHazardLevel? hazardLevel, string name)
        {
            if (sheetId <= 0)
            {
                return BuildResult(0, "success", new { items = new List<object>(), total = 0 });
            }
            var q = CheckSheetItem.Search(sheetId, hazardLevel,  name);
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        /// <summary>显示检查项（旧入口：保留兼容历史调用）</summary>
        public IActionResult OnPostShowItems([FromBody] CheckSheet req)
        {
            var sheetId = req?.Id ?? 0;
            if (sheetId <= 0)
            {
                return EleHandler.ShowNotify("请先保存检查表，再维护检查项", NotifyType.Warning, "提示");
            }

            var sheetName = Uri.EscapeDataString(req?.Name ?? string.Empty);
            var url = $"/Checks/CheckSheetItems?sheetId={sheetId}&sheetName={sheetName}&md={this.Mode}";
            return EleHandler.ShowDrawer(
                title: "检查项",
                url: url
                );
        }
    }
}
