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
            var list = CheckSheet.Search(name, scope).SortPageExport(pi);
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
