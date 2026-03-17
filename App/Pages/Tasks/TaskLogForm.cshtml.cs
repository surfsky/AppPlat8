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
    [CheckPower(Power.TaskEdit)]
    public class TaskLogFormModel : AdminModel
    {
        public AssignTaskLog Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id, long? taskId)
        {
            var item = AssignTaskLog.GetDetail(id);
            if (item == null)
            {
                item = new AssignTaskLog();
                if (taskId.HasValue) item.TaskId = taskId.Value;
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] AssignTaskLog req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? AssignTaskLog.Get(req.Id) : new AssignTaskLog();
            item.TaskId = req.TaskId;
            item.Handler = req.Handler;
            item.Progress = req.Progress;
            item.Status = req.Status;
            item.Save();

            // Update Task Progress
            var task = AssignTask.Get(item.TaskId);
            if (task != null)
            {
                task.Progress = item.Progress;
                task.Status = item.Status.Value;
                task.Save();
            }

            return BuildResult(0, "保存成功");
        }
    }
}
