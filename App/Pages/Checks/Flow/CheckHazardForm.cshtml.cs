using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using App.Components;
using App.DAL;
using App.EleUI;
using App.Entities;
using App.HttpApi;
using App.Utils;

namespace App.Pages.Checks
{
    [Auth(Power.CheckHazardEdit)]
    public class CheckHazardFormModel : AuthModel
    {
        /// <summary>隐患保存请求，显式承接图片 URL/Base64 列表。</summary>
        public class SaveRequest
        {
            public long Id { get; set; }
            public long? CheckObjectId { get; set; }
            public long? CheckLogId { get; set; }
            public long? CheckSheetId { get; set; }
            public long? CheckItemId { get; set; }
            public string CheckItemText { get; set; }
            public string Description { get; set; }
            public CheckHazardStatus? Status { get; set; }
            public DateTime? ExpireDt { get; set; }
            public DateTime? RectifyDt { get; set; }
            public bool? IsIn141 { get; set; }
            public List<string> ImageUrls { get; set; } = new List<string>();
        }

        public CheckHazard Item { get; set; }

        public void OnGet(long id = 0)
        {
            Item = CheckHazard.GetDetail(id) ?? new CheckHazard();
        }

        public IActionResult OnGetData(
            long id,
            long? objectId,
            long? checkLogId,
            long? checkSheetId,
            long? checkItemId,
            string checkItemText)
        {
            var item = id > 0 ? (CheckHazard.GetDetail(id) ?? new CheckHazard()) : new CheckHazard();
            if (id <= 0)
            {
                item.CheckObjectId = objectId;
                item.CheckLogId = checkLogId;
                item.CheckSheetId = checkSheetId;
                item.CheckItemId = checkItemId;
                item.CheckItemText = checkItemText;
            }

            var objectName = item.ObjectName;
            if (string.IsNullOrWhiteSpace(objectName) && item.CheckObjectId.HasValue)
                objectName = CheckObject.Get(item.CheckObjectId)?.Name ?? string.Empty;

            var sheetName = item.CheckSheetName;
            if (string.IsNullOrWhiteSpace(sheetName) && item.CheckSheetId.HasValue)
                sheetName = CheckSheet.Get(item.CheckSheetId)?.Name ?? string.Empty;

            var data = new
            {
                item.Id,
                item.CheckObjectId,
                ObjectName = objectName,
                item.CheckLogId,
                item.CheckSheetId,
                CheckSheetName = sheetName,
                item.CheckItemId,
                item.CheckItemText,
                item.Description,
                item.Status,
                item.StatusName,
                item.ExpireDt,
                item.RectifyDt,
                item.IsIn141,
                item.ImageUrls
            };

            return BuildResult(0, "success", data);
        }

        public IActionResult OnPostSave([FromBody] SaveRequest req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = CheckHazard.Get(req.Id);
            if (item == null)
            {
                item = new CheckHazard();
                item.CheckObjectId = req.CheckObjectId;
                item.CheckItemId = req.CheckItemId;
                item.CheckSheetId = req.CheckSheetId;
                item.CheckLogId = req.CheckLogId;
                item.CheckerId = GetUserId();
                item.CheckItemText = req.CheckItemText;
            }

            item.Description = req.Description;
            item.Status = req.Status;
            item.ExpireDt = req.ExpireDt;
            item.RectifyDt = req.RectifyDt;
            item.IsIn141 = req.IsIn141;
            item.Save();
            SyncHazardImages(item, req.ImageUrls);

            // 检查记录
            if (item.CheckLogId.HasValue)
            {
                var check = Check.Get(item.CheckLogId.Value);
                if (check == null)
                {
                    var userId = GetUserId();
                    var user = GetUser();
                    check = new Check
                    {
                        Id = item.CheckLogId.Value,
                        CreateDt = DateTime.Now,
                        CheckDt = DateTime.Now,
                        CheckObjectId = item.CheckObjectId,
                        CheckerId = userId,
                        OrgId = user?.OrgId,
                        HazardCount = 0,
                        RemainHazardCount = 0,
                        Result = false,
                    };
                    check.Save();
                }
            }

            return BuildResult(0, "保存成功");
        }

        /// <summary>按当前表单图片列表同步隐患图片附件，避免丢失或重复。</summary>
        static void SyncHazardImages(CheckHazard item, List<string> imageUrls)
        {
            var attIds = Att.Search(item.UniId, AttType.Image).Select(t => t.Id).ToList();
            if (attIds.Count > 0)
                Att.DeleteBatch(attIds);

            var savedUrls = Uploader.SaveFiles(nameof(CheckHazard), imageUrls)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
            foreach (var url in savedUrls)
                Att.Add(AttType.Image, item.UniId, url);
        }

        //-------------------------------------------------------
        // 隐患处理日志
        //-------------------------------------------------------
        public IActionResult OnGetLogsData(Paging pi, long hazardId)
        {
            if (hazardId <= 0)
                return BuildResult(0, "success", new { items = new List<object>(), total = 0 });

            var list = CheckHazardLog.Search(hazardId, null, null).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }


        //-------------------------------------------------------
        // 处理按钮（自动写处理日志 + 更新状态 + 刷新父级）
        //-------------------------------------------------------
        public IActionResult OnPostLog([FromBody] CheckHazard req)      => ProcessForm(req, CheckHazardStatus.Processing, "复查");
        public IActionResult OnPostFinish([FromBody] CheckHazard req)   => Process(req, CheckHazardStatus.Finished, "整改完成");
        public IActionResult OnPostMonitor([FromBody] CheckHazard req)  => ProcessForm(req, CheckHazardStatus.Monitor, "督查");
        public IActionResult OnPostArchive([FromBody] CheckHazard req)  => Process(req, CheckHazardStatus.Archived, "归档");

        // 弹窗处理
        private static IActionResult ProcessForm(CheckHazard req, CheckHazardStatus status, string action)
        {
            var hazardId = req?.Id ?? 0;
            if (hazardId <= 0)
                return EleHandler.ShowNotify("请先保存隐患，再录入处理记录", NotifyType.Warning, "提示");

            var url = $"/Checks/Flow/CheckHazardLogForm?hazardId={hazardId}&status={status}&content={action}";
            return EleHandler.ShowDrawer(title: $"{action}", url: url, closeAction: DrawerCloseAction.RefreshPage);
        }

        // 直接处理
        private IActionResult Process(CheckHazard req, CheckHazardStatus target, string action)
        {
            var hazardId = req?.Id ?? 0;
            if (hazardId <= 0) return BuildResult(400, "请先保存隐患");
            var hazard = CheckHazard.Get(hazardId);
            if (hazard == null) return BuildResult(404, "隐患不存在");

            var userId = GetUserId();
            var now = DateTime.Now;

            // 写处理历史（保证 EleList 里有记录可追踪）
            var log = new CheckHazardLog
            {
                HazardId = hazardId,
                ReviewerId = userId,
                ReviewDt = now,
                Content = action,
                Status = target,
                Image = null,
            };
            log.Save();

            // 更新主表状态
            hazard.Status = target;
            if (target == CheckHazardStatus.Finished) hazard.RectifyDt = now;
            if (target == CheckHazardStatus.Processing && hazard.RectifyDt == null) hazard.RectifyDt = null;
            hazard.Save();

            return EleHandler.BuildCommandsResult(
                new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Success, action + "成功", "提示")),
                new ClientCommand(ClientCommandType.RefreshData, new RefreshDataArgs(RefreshScope.Parent))
            );
        }

    }
}
