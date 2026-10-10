using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [Auth(Power.TaskView)]
    public class TasksModel : AuthModel
    {
        public App.DAL.OA.AssignTask Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(Paging pi, string name, long? orgId)
        {
            var cu = Auth.GetUser();
            var q = AssignTask.Search(name, orgId);
            if (!Auth.IsAdmin(cu))
                q = q.FilterByOrg(cu);
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.TaskDelete))
                return BuildResult(403, "无权操作");

            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);
            var authOrgId = cu?.EffectiveAuthOrgId;
            foreach (var id in ids)
            {
                var target = AssignTask.Get(id);
                if (target == null) continue;
                if (!isAdmin && !OrgFilter.IsAuth(authOrgId, target.OrgId))
                    return BuildResult(403, $"无权删除记录（Id={id}）");
                AssignTask.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }
    }
}
