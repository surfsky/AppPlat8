using System.Collections.Generic;
using System.Linq;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.Pages.EleUIDemo
{
    public class ListPickerModel : BaseModel
    {
        public ListPickerDemoItem Item { get; set; } = new();
        public List<SelectListItem> TaskOptions { get; private set; } = new();
        public string MultiDefault { get; private set; } = "101,103";
        public string SingleDefault { get; private set; } = "102";

        public void OnGet()
        {
            TaskOptions = BuildOptions();
        }

        public IActionResult OnGetData(Paging pi, List<long> taskIds, long? taskId)
        {
            var row = new ListPickerDemoItem
            {
                Name = "筛选值已传到 EleTable 的 Data handler",
                SelectedTaskIds = string.Join(", ", taskIds ?? new List<long>()),
                SelectedTaskId = taskId?.ToString() ?? "（空）"
            };
            pi.SetTotal(1);
            return BuildResult(0, "success", new[] { row }, pi);
        }

        public IActionResult OnGetFormData()
        {
            return BuildResult(0, "success", new
            {
                formTaskIds = new long[] { 101, 103 },
                formTaskId = 102
            });
        }

        public IActionResult OnPostSave([FromBody] ListPickerDemoItem values)
        {
            return BuildResult(0, "表单当前值", values);
        }

        private static List<SelectListItem> BuildOptions() => new()
        {
            new SelectListItem("安全生产专项检查", "101"),
            new SelectListItem("消防安全专项检查", "102"),
            new SelectListItem("危化品专项检查", "103"),
            new SelectListItem("有限空间专项检查", "104"),
        };
    }

    public class ListPickerDemoItem
    {
        public string Name { get; set; }
        public List<long> TaskIds { get; set; } = new();
        public long? TaskId { get; set; }
        public string SelectedTaskIds { get; set; }
        public string SelectedTaskId { get; set; }
        public List<long> FormTaskIds { get; set; } = new();
        public long? FormTaskId { get; set; }
    }
}
