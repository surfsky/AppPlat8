using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.ProjectEdit)]
    public class ProjectLogFormModel : AdminModel
    {
        public ProjectLog Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id, long? projectId)
        {
            var item = ProjectLog.GetDetail(id);
            if (item == null)
            {
                item = new ProjectLog();
                if (projectId.HasValue) item.ProjectId = projectId.Value;
                item.LogDt = DateTime.Now;
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] ProjectLog req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = ProjectLog.Get(req.Id);
            if (item == null)
            {
                item = new ProjectLog();
                item.CreateDt = DateTime.Now;
            }

            item.ProjectId = req.ProjectId;
            item.LogDt = req.LogDt;
            item.Person = req.Person;
            item.Status = req.Status;
            item.Progress = req.Progress;
            item.Description = req.Description;

            item.Save();
            
            // Update Project Progress
            var project = Project.Get(item.ProjectId);
            if (project != null)
            {
                project.Progress = item.Progress;
                project.Save();
            }

            return BuildResult(0, "保存成功");
        }
    }
}
