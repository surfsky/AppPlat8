using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckLogEdit)]
    public class CheckLogFormModel : AdminModel
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
            var item = Check.Get(id);
            if (item == null)
            {
                item = new Check();
                item.CheckDt = DateTime.Now;
                item.CheckerId = GetUserId();
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] Check req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = Check.Get(req.Id);
            if (item == null)
            {
                item = new Check();
                item.CreateDt = DateTime.Now;
            }

            item.TaskId = req.TaskId;
            item.CheckDt = req.CheckDt;
            item.OrgId = req.OrgId;
            item.CheckerId = req.CheckerId;
            item.CheckObjectId = req.CheckObjectId;
            item.Result = req.Result;
            item.HazardCount = req.HazardCount;
            item.IsClosed = req.IsClosed;
            item.HazardCount = req.HazardCount;

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
