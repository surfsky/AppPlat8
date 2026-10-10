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
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.Pages.Checks
{
    [Auth(Power.CheckEdit)]
    public class CheckFormModel : AuthModel
    {
        public Check Item { get; set; }
        public List<SelectListItem> CheckObjects { get; set; }
        public List<SelectListItem> Tasks { get; set; }

        public void OnGet()
        {
            CheckObjects = CheckObject.Set.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name }).ToList();
            Tasks = CheckTask.Set.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name }).ToList();
        }

        public IActionResult OnGetData(long id)
        {
            var item = Check.GetDetail(id);
            if (item == null)
            {
                item = new Check();
                item.CheckDt = DateTime.Now;
                item.CheckerId = GetUserId();
            }
            else if (item.Id > 0)
            {
                var cu = Auth.GetUser();
                if (!Auth.IsAdmin(cu) && !OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, item.OrgId))
                    return BuildResult(403, "越权访问");
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] Check req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = Check.GetDetail(req.Id);
            if (item == null)
            {
                item = new Check();
                item.CreateDt = DateTime.Now;
            }
            else
            {
                var cu = Auth.GetUser();
                if (!Auth.IsAdmin(cu) && !OrgFilter.IsAuth(cu?.EffectiveAuthOrgId, item.OrgId))
                    return BuildResult(403, "越权修改");
            }

            item.CheckDt = req.CheckDt;
            item.OrgId = req.OrgId;
            item.CheckerId = req.CheckerId;
            item.CheckObjectId = req.CheckObjectId;
            item.CheckSheetId = req.CheckSheetId;
            item.CheckItemId = req.CheckItemId;
            item.Result = req.Result;
            item.HazardCount = req.HazardCount;
            item.RemainHazardCount = req.RemainHazardCount;
            item.IsClosed = req.IsClosed;
            item.SetTasks(req.TaskIds);

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
