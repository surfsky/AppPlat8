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

        public void OnGet(long id = 0)
        {
            Item = Check.Get(id) ?? new Check();
            if (Mode == PageMode.View && id > 0)
            {
                // View 模式只加载当前记录关联的对象和任务，避免全表扫描导致卡死
                var check = Check.GetDetail(id);
                CheckObjects = check?.CheckObject != null
                    ? new List<SelectListItem> { new SelectListItem(check.CheckObject.Name, check.CheckObject.Id.ToString()) }
                    : new List<SelectListItem>();
                Tasks = check?.Tasks?.Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList()
                        ?? new List<SelectListItem>();
            }
            else
            {
                CheckObjects = CheckObject.Set.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name }).ToList();
                Tasks = CheckTask.Set.Select(s => new SelectListItem { Value = s.Id.ToString(), Text = s.Name }).ToList();
            }
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
            item.SetTasks(req.TaskIds);

            item.Save();
            return BuildResult(0, "保存成功");
        }

        /// <summary>获取隐患清单列表数据</summary>
        public IActionResult OnGetHazardsData(Paging pi, long checkId)
        {
            if (checkId <= 0) return BuildResult(0, "success", new { items = new List<object>(), total = 0 });
            var list = CheckHazard.IncludeSet
                .Include(h => h.Check)
                .Where(h => h.CheckLogId == checkId)
                .SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }
    }
}
