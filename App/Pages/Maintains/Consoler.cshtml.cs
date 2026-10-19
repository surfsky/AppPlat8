using System;
using App.Components;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Maintains
{
    /// <summary>控制台调度器维护页：启动、停止并查看 App.Consoler 输出。</summary>
    public partial class ConsolerModel : AuthModel
    {
        [BindProperty] public string ProjectPath { get; set; }
        [BindProperty] public string WorkingDirectory { get; set; }

        /// <summary>初始化页面静态信息。</summary>
        public void OnGet()
        {
            var status = ConsolerRunner.GetStatus(GetApiBaseUrl());
            ProjectPath = status.ProjectPath;
            WorkingDirectory = status.WorkingDirectory;
        }

        /// <summary>读取调度器当前状态与输出。</summary>
        public IActionResult OnGetStatus()
        {
            return BuildResult(0, "success", ConsolerRunner.GetStatus(GetApiBaseUrl()));
        }

        /// <summary>启动调度器进程。</summary>
        public IActionResult OnPostStart()
        {
            var status = ConsolerRunner.Start(GetApiBaseUrl(), out var error);
            return string.IsNullOrWhiteSpace(error)
                ? BuildResult(0, "调度器已启动", status)
                : BuildResult(400, error, status);
        }

        /// <summary>停止调度器进程。</summary>
        public IActionResult OnPostStop()
        {
            var status = ConsolerRunner.Stop(out var error);
            return string.IsNullOrWhiteSpace(error)
                ? BuildResult(0, "调度器已停止", status)
                : BuildResult(400, error, status);
        }

        /// <summary>清空当前缓存的控制台输出。</summary>
        public IActionResult OnPostClearOutput()
        {
            return BuildResult(0, "输出已清空", ConsolerRunner.ClearOutput(GetApiBaseUrl()));
        }

        /// <summary>拼出当前站点根地址，供 App.Consoler 回调 API 使用。</summary>
        private string GetApiBaseUrl()
        {
            if (Request?.Host.HasValue == true)
                return $"{Request.Scheme}://{Request.Host}";
            return "http://localhost:6001";
        }

 
    }
}
