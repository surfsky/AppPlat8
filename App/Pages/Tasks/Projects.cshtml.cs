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
    [CheckPower(Power.ProjectView)]
    public class ProjectsModel : AdminModel
    {
        public Project Item { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnGetData(Paging pi, string name, long? orgId)
        {
            var list = Project.Search(name, orgId).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.ProjectDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Project.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
