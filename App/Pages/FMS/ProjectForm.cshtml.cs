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
    [Auth(Power.ProjectEdit)]
    public class ProjectFormModel : AuthModel
    {
        public Project Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id)
        {
            var item = Project.GetDetail(id) ?? new Project();
            if (id > 0 && item != null && item.Id > 0)
            {
                var cu = Auth.GetUser();
                if (!Auth.IsAdmin(cu) && !OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, item.OrgId))
                    return BuildResult(403, "越权访问");
            }
            return BuildResult(0, "success", item.Export(ExportMode.Normal));
        }

        public IActionResult OnPostSave([FromBody] Project req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? Project.Get(req.Id) : new Project();
            if (req.Id > 0)
            {
                var cu = Auth.GetUser();
                if (!Auth.IsAdmin(cu) && !OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, item?.OrgId))
                    return BuildResult(403, "越权修改");
            }
            item.Name = req.Name;
            item.Alias = req.Alias;
            item.OrgId = req.OrgId;
            item.PersonInCharge = req.PersonInCharge;
            item.DevCompany = req.DevCompany;
            item.MaintCompany = req.MaintCompany;
            item.ContractDt = req.ContractDt;
            item.Progress = req.Progress;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
