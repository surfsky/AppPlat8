using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using App.EleUI;
using BootstrapBlazor.Components;

namespace App.Pages.Shared
{
    [Auth(Power.CheckObjectView)]
    public class AttsMoveToModel : AdminModel
    {
        public long[] SelectedIds { get; set; } = Array.Empty<long>();
        public string UniId { get; set; } = "";
        public string CurrentMenuIdStr { get; set; } = "0";
        public string MenuTreeJson { get; set; } = "[]";
        public string SelectedIdsJson { get; set; } = "[]";
        public string UniIdJson { get; set; } = "\"\"";
        public int SelectedCount => SelectedIds?.Length ?? 0;

        private JsonResult OkBuildResult(int code, string msg, object data = null)
        {
            var json = BuildResult(code, msg, data);
            json.StatusCode = 200;
            return json;
        }

        public void OnGet(string uniId, string ids)
        {
            UniId = uniId ?? "";
            SelectedIds = (ids ?? "")
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s.Trim(), out var v) ? (long?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .ToArray();

            if (UniId.StartsWith("KbMenu-", StringComparison.OrdinalIgnoreCase))
            {
                var tail = UniId.Substring("KbMenu-".Length);
                if (long.TryParse(tail, out var curId))
                    CurrentMenuIdStr = curId.ToString();
            }

            var tree = App.DAL.KbMenu.GetTree();
            MenuTreeJson = tree.ToJson();
            SelectedIdsJson = SelectedIds.ToJson();
            UniIdJson = UniId.ToJson();
        }

        // 在 Drawer 页面内直接完成移动操作，避免跨 iframe postMessage 链路失败
        public IActionResult OnPostMoveTo([FromBody] MoveToRequest req)
        {
            if (req?.Ids == null || req.Ids.Length == 0)
                return OkBuildResult(400, "请先勾选要移动的附件");
            req.UniId = req.UniId?.Trim();
            if (string.IsNullOrWhiteSpace(req.UniId))
                return OkBuildResult(400, "缺少源目录Key");
            if (req.TargetMenuId <= 0)
                return OkBuildResult(400, "请选择目标目录");
            if (!CheckPower(Power.CheckObjectEdit))
                return OkBuildResult(403, "无权移动附件");

            if (!req.UniId.StartsWith("KbMenu-", StringComparison.OrdinalIgnoreCase))
                return OkBuildResult(400, "当前页面不是知识库目录，不支持移动");

            var target = App.DAL.KbMenu.Get(req.TargetMenuId);
            if (target == null)
                return OkBuildResult(404, "目标目录不存在或已删除");

            var targetKey = $"KbMenu-{req.TargetMenuId}";
            if (string.Equals(req.UniId, targetKey, StringComparison.OrdinalIgnoreCase))
                return OkBuildResult(400, "目标目录与源目录相同");

            var toMove = Att.Set.Where(t => req.Ids.Contains(t.Id) && t.Key == req.UniId).ToList();
            var affected = 0;
            foreach (var a in toMove)
            {
                a.Key = targetKey;
                a.Save();
                affected++;
            }

            // 客户端命令序列：Toast → 关抽屉 → 刷新父 iframe 的 EleTable 列表
            // 关键：RefreshDataArgs 携带源 UniId（KbMenu-78），前端刷新命中 Atts 列表时按 uniId 精确匹配
            var msg = affected > 0 ? $"移动成功，共{affected}个附件" : "没有可移动的附件（可能已被移动或权限不足）";
            return App.EleUI.EleServer.BuildCommandResult(
                new ClientCommand(
                    ClientCommandType.Toast,
                    new NotifyArgs(
                        Type: affected > 0 ? NotifyType.Success : NotifyType.Warning,
                        Message: msg,
                        Title: "移动附件"
                    )
                ),
                new ClientCommand(ClientCommandType.CloseDrawer, new { }),
                new ClientCommand(
                    ClientCommandType.RefreshData,
                    new
                    {
                        scope = RefreshScope.Parent,
                        uniId = req.UniId,
                        targetMenuId = req.TargetMenuId,
                        instanceId = (string)null
                    }
                )
            );
        }

        public class MoveToRequest
        {
            public long[] Ids { get; set; }
            public string UniId { get; set; }
            public long TargetMenuId { get; set; }
        }
    }
}
