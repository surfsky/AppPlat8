using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using App.Utils;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;

namespace App.Pages.EleUISamples
{
    public class ButtonModel : BaseModel
    {
        static readonly JsonSerializerOptions SseJsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

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

        /// <summary>模拟流式长任务：用于演示 EleButton 的 SSE 进度弹层效果。</summary>
        public async Task<IActionResult> OnPostFixData()
        {
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            await WriteSseEventAsync(new { percent = 0, message = "开始处理示例任务..." });

            const int total = 5;
            for (var i = 1; i <= total; i++)
            {
                await Task.Delay(600);
                await WriteSseEventAsync(new
                {
                    percent = i * 100 / total,
                    message = $"已处理 {i}/{total}"
                });
            }

            await WriteSseEventAsync(new
            {
                code = 0,
                message = "示例流式任务执行完成",
                done = true,
                percent = 100,
                data = new
                {
                    finishedAt = DateTime.Now.ToString("HH:mm:ss")
                }
            });

            return new EmptyResult();
        }

        /// <summary>输出一条 SSE 事件并立即刷新响应。</summary>
        async Task WriteSseEventAsync(object payload)
        {
            var json = JsonSerializer.Serialize(payload, SseJsonOptions);
            var buffer = Encoding.UTF8.GetBytes($"data: {json}\n\n");
            await Response.Body.WriteAsync(buffer, 0, buffer.Length);
            await Response.Body.FlushAsync();
        }
    }
}
