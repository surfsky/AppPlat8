using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.EleUI;
using App.DAL;
using App.Utils;
using App.Components;
using App.Web;
using App.HttpApi;
using App.Entities;
using System;

namespace App.Pages.Admins
{
    using User = App.DAL.User; // Fix conflict with PageModel.User

    [CheckPower(Power.UserView)]
    public class UsersModel : AdminModel
    {
        public App.DAL.User Item { get; set; }

        public void OnGet()
        {
        }

        /// <summary>获取用户列表</summary>
        public async Task<IActionResult> OnGetData(Paging pi, string name, string realName, long? deptId)
        {
            var list = App.DAL.User.Search(name, realName, deptId).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        // 导出用户列表到 Excel
        public async Task<IActionResult> OnPostExport(Paging pi, string name, string username, long? deptId)
        {
            var exportPi = new Paging { PageIndex = 1, PageSize = int.MaxValue }; // 导出所有匹配的数据（不分页）
            var list = App.DAL.User.Search(name, username, deptId).SortPageExport(exportPi);
            ExcelExporter.Export(list, $"用户列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            return new EmptyResult();
        }

        // 私有方法：搜索用户列表

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0) return BuildResult(400, "参数错误");
            foreach (var id in ids)
            {
                if (id == 1) continue;
                App.DAL.User.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }

    }
}
