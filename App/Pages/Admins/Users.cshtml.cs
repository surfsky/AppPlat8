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
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.Pages.Admins
{
    using User = App.DAL.User; // Fix conflict with PageModel.User

    [Auth(Power.UserView)]
    public class UsersModel : AdminModel
    {
        public App.DAL.User Item { get; set; }
        public List<SelectListItem> RoleList { get; set; }

        public void OnGet()
        {
            RoleList = Role.Set
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Select(r => new SelectListItem(r.Name, r.Id.ToString()))
                .ToList();
        }

        /// <summary>获取用户列表</summary>
        public IActionResult OnGetData(Paging pi, string name, string realName, long? deptId, long? roleId)
        {
            var list = App.DAL.User.Search(name, realName, deptId, roleId).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        // 导出用户列表到 Excel
        public IActionResult OnPostExport(Paging pi, string name, string realName, long? deptId, long? roleId)
        {
            var exportPi = new Paging { PageIndex = 1, PageSize = int.MaxValue, SortField = pi.SortField, SortDirection = pi.SortDirection }; // 导出所有匹配的数据（不分页）,保持与页面上相同的排序
            var list = App.DAL.User.Search(name, realName, deptId, roleId).SortPageExport(exportPi);
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

        public IActionResult OnPostImport()
        {
            if (!CheckPower(Power.UserNew))
                return BuildResult(403, "无权操作");

            var url = "/Shared/Importor?type=" + Uri.EscapeDataString("App.DAL.User");
            return EleManager.ShowDrawer(
                title: "导入用户",
                url: url,
                //size: "980px",
                direction: "rtl",
                closeAction: DrawerCloseAction.RefreshData);
        }
    }
}
