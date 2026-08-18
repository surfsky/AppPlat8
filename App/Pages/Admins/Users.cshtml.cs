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
        public long? RoleId { get; set; }
        public List<SelectListItem> RoleList { get; set; }
        public bool CanResetPassword { get; set; }

        public void OnGet()
        {
            RoleList = Role.Set
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Id)
                .Select(r => new SelectListItem(r.Name, r.Id.ToString()))
                .ToList();
            // 获取当前登录用户是否可以重置密码权限
            CanResetPassword = this.CheckPower(Power.UserResetPassword);
            //CanResetPassword = string.Equals(GetUserName(), "admin", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>获取用户列表</summary>
        public IActionResult OnGetData(Paging pi, string name, string realName, long? deptId, long? roleId, bool? isDel)
        {
            var list = App.DAL.User.Search(name, realName, deptId, roleId, isDel).SortPageExport(pi);
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
                if (id == 1) continue;   // admin 管理员用户不能删除
                App.DAL.User.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }

        public IActionResult OnPostImport()
        {
            if (!CheckPower(Power.UserNew))
                return BuildResult(403, "无权操作");

            var url = "/Shared/Importor?type=" + Uri.EscapeDataString("App.DAL.User");
            return EleHandler.ShowDrawer(
                title: "导入用户",
                url: url,
                //size: "980px",
                direction: "rtl",
                closeAction: DrawerCloseAction.RefreshData);
        }

        /// <summary>管理员将用户密码重置为默认密码。</summary>
        public IActionResult OnPostResetPasswordToDefault([FromBody] ResetPasswordToDefaultRequest req)
        {
            Logger.Info($"ResetPasswordToDefault: {req.Id}");
            if (!string.Equals(GetUserName(), "admin", StringComparison.OrdinalIgnoreCase))
                return BuildResult(403, "仅管理员可重置默认密码");

            if (req == null || req.Id <= 0)
                return BuildResult(400, "参数错误");

            var user = App.DAL.User.Get(req.Id);
            if (user == null)
                return BuildResult(404, "用户不存在");
            if (user.Name == "admin")
                return BuildResult(403, "管理员用户不能在此重置密码");

            var defaultPassword = SiteConfig.Instance.DefaultPassword?.Trim();
            if (string.IsNullOrWhiteSpace(defaultPassword))
                return BuildResult(500, "系统默认密码未配置");

            user.Password = PasswordUtil.CreateDbPassword(defaultPassword);
            user.Save();
            //return EleHandler.ShowToast($"已将用户“{user.Name}”的密码重置为默认密码{defaultPassword}");
            return BuildResult(0, $"已将用户“{user.Name}”的密码重置为默认密码{defaultPassword}");
        }

        public class ResetPasswordToDefaultRequest
        {
            public long Id { get; set; }
        }
    }
}
