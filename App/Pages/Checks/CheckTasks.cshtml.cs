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
    [CheckPower(Power.CheckTaskView)]
    public class CheckTasksModel : AdminModel
    {
        public CheckTask Item { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnGetData(Paging pi, string name, long? publisherId, DateTime? expireBefore)
        {
            var list = CheckTask.Search(name, publisherId, expireBefore).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckTaskDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                CheckTask.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
