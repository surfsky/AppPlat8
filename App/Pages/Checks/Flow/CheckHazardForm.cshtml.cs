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
            public CheckHazardRectifyType? RectifyType { get; set; }
            public DateTime? ExpireDt { get; set; }
            public DateTime? RectifyDt { get; set; }
            public bool? IsIn141 { get; set; }
            public List<string> ImageUrls { get; set; } = new List<string>();
        }

        public CheckHazard Item { get; set; }

        //---------------------------------------------------------------------
        // 初始化
        //---------------------------------------------------------------------
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
                item.ExpireDt = DateTime.Now.AddMonths(1);
                item.Status = CheckHazardStatus.Waiting;
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
                item.RectifyType,
                item.ExpireDt,
                item.RectifyDt,
                item.IsIn141,
                item.ImageUrls
            };

            return BuildResult(0, "success", data);
        }

        //---------------------------------------------------------------------
        // 整改方式联动
        //---------------------------------------------------------------------
        /// <summary>整改方式变更：立查立改时隐藏“整改期限”（值保留），其余情况显示</summary>
        public IActionResult OnPostRectifyTypeChanged([FromBody] ControlChangeRequest req)
        {
            var type = Enum.TryParse<CheckHazardRectifyType>(req?.Value?.ToString(), true, out var parsed) ? parsed : (CheckHazardRectifyType?)null;
            return EleHandler
                .SetControl<CheckHazard>(t => t.ExpireDt, Visible: type != CheckHazardRectifyType.Direct)
                .ToActionResult();
        }

        //---------------------------------------------------------------------
        // 保存隐患
        //---------------------------------------------------------------------
        public IActionResult OnPostSave([FromBody] SaveRequest req)
        {
            var item = SaveHazard(req, out var error);
            return item == null ? BuildResult(400, error) : BuildResult(0, "保存成功");
        }

        /// <summary>保存隐患并按整改方式分流：限期整改关闭窗口；立查立改关闭当前窗口后弹出整改窗口</summary>
        public IActionResult OnPostSaveRectify([FromBody] SaveRequest req)
        {
            var item = SaveHazard(req, out var error);
            if (item == null)
                return BuildResult(400, error);

            var toast = new ClientCommand(ClientCommandType.Toast, new NotifyArgs(NotifyType.Success, "保存成功", "提示"));
            var isDirect = req?.RectifyType == CheckHazardRectifyType.Direct || item.RectifyType == CheckHazardRectifyType.Direct;

            // 立查立改：先关闭当前隐患窗口，再弹出整改窗口，避免叠加抽屉链路不稳定。
            if (isDirect)
            {
                var content = CheckHazardRectifyType.Direct.GetTitle();
                var url = $"/Checks/Flow/CheckHazardLogForm?hazardId={item.Id}&status={(int)CheckHazardStatus.Finished}&content={Uri.EscapeDataString(content)}";
                return EleHandler.BuildCommandsResult(
                    toast,
                    new ClientCommand(ClientCommandType.CloseDrawer, new { }),
                    new ClientCommand(ClientCommandType.Sleep, 150),
                    new ClientCommand(ClientCommandType.OpenDrawer, new DrawerArgs(Title: content, Url: url, CloseAction: DrawerCloseAction.RefreshPage))
                );
            }

            // 限期整改：正常关闭窗口
            return EleHandler.BuildCommandsResult(
                toast,
                new ClientCommand(ClientCommandType.CloseDrawer, new { })
            );
        }

        /// <summary>隐患保存公共逻辑；返回 null 表示失败（原因见 error）</summary>
        private CheckHazard SaveHazard(SaveRequest req, out string error)
        {
            error = null;
            if (req == null)
            {
                error = "参数错误";
                return null;
            }

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
            item.RectifyType = req.RectifyType;
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

            return item;
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
        // 处理流程按钮（自动写处理日志 + 更新状态 + 刷新父级）
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

            var url = $"/Checks/Flow/CheckHazardLogForm?hazardId={hazardId}&status={(int)status}&content={action}";
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
                //new ClientCommand(ClientCommandType.RefreshData, new RefreshDataArgs(RefreshScope.Parent)),
                new ClientCommand(ClientCommandType.Sleep, 1000),
                new ClientCommand(ClientCommandType.RefreshPage, new RefreshPageArgs(RefreshScope.Self))
            );
        }

    }
}
