using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using App.Utils;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;

namespace App.Pages.EleUISamples
{
    public class ButtonModel : BaseModel
    {
        [BindProperty]
        public bool ShowItem {get;set;} = true;

        public class SwitchRequest
        {
            public bool ShowItem { get; set; }
        }

        public void OnGet(){}

        public IActionResult OnPostSwitch([FromBody] SwitchRequest data)
        {
            var next = !(data?.ShowItem ?? false);
            return BuildResult(0, "切换成功", new { showItem = next });
        }

        /// <summary>模拟耗时操作：用于演示 EleButton 的 AutoLoading 自动 loading 效果</summary>
        public async Task<IActionResult> OnPostLongTask()
        {
            await Task.Delay(3000);
            return BuildResult(0, "耗时操作已完成", new { finishedAt = DateTime.Now.ToString("HH:mm:ss") });
        }
    }
}