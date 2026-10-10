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
    [Auth(Power.TaskEdit)]
    public class TaskFormModel : AuthModel
    {
        public AssignTask Item { get; set; }
        public List<App.DAL.Org> OrgTree { get; set; }

        public void OnGet()
        {
            OrgTree = App.DAL.Org.GetTree();
        }

        public IActionResult OnGetData(long id)
        {
            var item = App.DAL.OA.AssignTask.GetDetail(id) ?? new App.DAL.OA.AssignTask();
            if (id > 0 && item != null && item.Id > 0)
            {
                var cu = Auth.GetUser();
                if (!Auth.IsAdmin(cu) && !OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, item.OrgId))
                    return BuildResult(403, "越权访问");
            }
            return BuildResult(0, "success", item.Export(ExportMode.Normal));
        }

        public IActionResult OnPostSave([FromBody] App.DAL.OA.AssignTask req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? AssignTask.Get(req.Id) : new AssignTask();
            if (req.Id > 0)
            {
                var cu = Auth.GetUser();
                if (!Auth.IsAdmin(cu) && !OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, item?.OrgId))
                    return BuildResult(403, "越权修改");
            }
            item.Name = req.Name;
            item.Initiator = req.Initiator;
            item.PersonInCharge = req.PersonInCharge;
            item.OrgId = req.OrgId;
            item.Cycle = req.Cycle;
            item.StartDt = req.StartDt;
            item.NextReportDt = req.NextReportDt;
            item.Progress = req.Progress;
            item.Status = req.Status;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
