using System;
using App.Components;
using App.DAL;
using App.EleUI;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.Checks
{
    [Auth(Power.CheckHazardEdit)]
    public class CheckHazardLogFormModel : AuthModel
    {
        public CheckHazardLog Item { get; set; }

        public void OnGet(long hazardId, long id = 0, CheckHazardStatus? status = null, string content = null)
        {
            Item = id > 0
                ? (CheckHazardLog.Get(id) ?? new CheckHazardLog())
                : new CheckHazardLog { HazardId = hazardId, ReviewDt = DateTime.Now };
            FillFromUrl(Item, status, content);
        }

        public IActionResult OnGetData(long hazardId, long id = 0, CheckHazardStatus? status = null, string content = null)
        {
            var item = id > 0
                ? (CheckHazardLog.Get(id) ?? new CheckHazardLog())
                : new CheckHazardLog { HazardId = hazardId, ReviewDt = DateTime.Now };
            FillFromUrl(item, status, content);

            var data = new
            {
                item.Id,
                item.HazardId,
                item.Content,
                item.Status,
                item.ReviewDt,
                item.Image,
            };
            return BuildResult(0, "success", data);
        }

        /// <summary>用 URL 参数预填状态与处理内容（复查/督查入口会带 status + content 跳转过来）。</summary>
        static void FillFromUrl(CheckHazardLog item, CheckHazardStatus? status, string content)
        {
            if (item == null) return;
            if (status.HasValue) item.Status = status;
            if (!string.IsNullOrWhiteSpace(content)) item.Content = content;
        }

        public IActionResult OnPostSave([FromBody] CheckHazardLog req)
        {
            if (req == null || req.HazardId <= 0)
                return BuildResult(400, "参数错误");

            var hazard = CheckHazard.Get(req.HazardId);
            if (hazard == null)
                return BuildResult(404, "隐患不存在");

            var item = req.Id > 0 ? CheckHazardLog.Get(req.Id) : null;
            if (item == null)
            {
                item = new CheckHazardLog
                {
                    HazardId = req.HazardId,
                    ReviewerId = GetUserId(),
                };
            }

            item.Content = req.Content?.Trim();
            item.Status = req.Status;
            item.ReviewDt = req.ReviewDt ?? DateTime.Now;
            item.Image = Uploader.SaveFile(nameof(CheckHazardLog), req.Image);
            item.Save();

            hazard.Status = req.Status ?? hazard.Status;
            hazard.RectifyDt = req.Status == CheckHazardStatus.Finished
                ? (req.ReviewDt ?? DateTime.Now)
                : hazard.RectifyDt;
            hazard.Save();

            return EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Success, "处理记录保存成功", "成功")),
                new ClientCommand(ClientCommandType.CloseDrawer, new { }),
                new ClientCommand(ClientCommandType.RefreshData, new RefreshDataArgs(RefreshScope.Parent))
            );
        }
    }
}