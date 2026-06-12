using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using App.Components;
using App.DAL;
using App.EleUI;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckNew)]
    public class CheckFlowModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long? ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        public CheckFlowData Item { get; set; }

        public List<SelectListItem> TaskOptions { get; set; } = new();
        public List<SelectListItem> SheetOptions { get; set; } = new();

        public void OnGet(long? objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;

            TaskOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = string.Empty, Text = "日常检查" }
            };
            TaskOptions.AddRange(
                CheckTask.Set
                    .OrderBy(t => t.Id)
                    .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.Name })
                    .ToList()
            );

            SheetOptions = CheckSheet.Set
                .OrderBy(t => t.Id)
                .Select(t => new SelectListItem { Value = t.Id.ToString(), Text = t.Name })
                .ToList();
        }

        public IActionResult OnGetData(long? objectId)
        {
            var id = objectId ?? ObjectId ?? 0;
            if (id <= 0)
                return BuildResult(400, "缺少检查对象参数");

            var obj = CheckObject.IncludeSet
                .Include(t => t.DutyOrg)
                .Include(t => t.Tags)
                .ThenInclude(t => t.Tag)
                .FirstOrDefault(t => t.Id == id);
            if (obj == null)
                return BuildResult(404, "检查对象不存在");

            var user = GetUser();
            var org = user?.OrgId != null ? App.DAL.Org.Get(user.OrgId) : null;

            var checkLogId = SnowflakeId.Instance.NewId();
            var tagNames = obj.TagNames != null && obj.TagNames.Count > 0
                ? string.Join("，", obj.TagNames)
                : string.Empty;

            Item = new CheckFlowData
            {
                ObjectId = obj.Id,
                ObjectName = obj.Name ?? string.Empty,
                DutyOrgName = obj.DutyOrgName ?? string.Empty,
                Address = obj.Address ?? string.Empty,
                RiskLevelName = obj.RiskLevel?.GetTitle() ?? string.Empty,
                ScopeName = obj.Scope?.GetTitle() ?? string.Empty,
                TagNames = tagNames,
                CheckLogId = checkLogId,
                CheckDt = DateTime.Now,
                TaskId = null,
                PatrolOrgName = org?.Name ?? string.Empty,
                PatrolOrgLevel = org?.Level?.GetTitle() ?? string.Empty,
                SheetIds = new List<long>(),
                CheckItems = new List<CheckFlowItemRow>()
            };

            return BuildResult(0, "success", Item);
        }

        public IActionResult OnPostSheetItems([FromBody] ControlChangeRequest req)
        {
            var sheetIds = new List<long>();
            if (req != null)
            {
                if (req.Form.ValueKind == JsonValueKind.Object && req.Form.TryGetProperty("sheetIds", out var sheetIdsEl))
                {
                    sheetIds = ReadLongList(sheetIdsEl);
                }
                else if (req.Value is JsonElement valueEl)
                {
                    sheetIds = ReadLongList(valueEl);
                }
            }

            sheetIds = sheetIds.Where(t => t > 0).Distinct().ToList();
            if (sheetIds.Count == 0)
                return BuildResult(0, "success", new { checkItems = new List<CheckFlowItemRow>() });

            var sheetNameMap = CheckSheet.Set
                .Where(t => sheetIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Name })
                .ToDictionary(t => t.Id, t => t.Name ?? string.Empty);

            var items = CheckSheetItem.Set
                .Where(t => sheetIds.Contains(t.SheetId))
                .OrderBy(t => t.SheetId)
                .ThenBy(t => t.SortId)
                .Select(t => new
                {
                    t.Id,
                    t.SheetId,
                    t.Name,
                    t.HazardLevel
                })
                .ToList();

            var rows = items.Select(t => new CheckFlowItemRow
            {
                CheckItemId = t.Id,
                SheetId = t.SheetId,
                SheetName = sheetNameMap.TryGetValue(t.SheetId, out var name) ? name : string.Empty,
                Name = t.Name ?? string.Empty,
                HazardLevelName = t.HazardLevel?.GetTitle() ?? string.Empty,
                Result = "pass",
                Remark = string.Empty
            }).ToList();

            return BuildResult(0, "success", new { checkItems = rows });
        }

        public IActionResult OnPostSave([FromBody] CheckFlowData req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (req.ObjectId <= 0)
                return BuildResult(400, "检查对象无效");
            if (req.CheckLogId <= 0)
                return BuildResult(400, "检查记录标识无效");

            var userId = GetUserId();
            if (userId == null)
                return BuildResult(401, "请先登录");

            var user = GetUser();
            var now = DateTime.Now;
            var checkDt = req.CheckDt == default ? now : req.CheckDt;

            var check = Check.Get(req.CheckLogId);
            if (check == null)
            {
                check = new Check
                {
                    Id = req.CheckLogId,
                    CreateDt = now
                };
            }

            check.TaskId = req.TaskId;
            check.CheckDt = checkDt;
            check.OrgId = user?.OrgId;
            check.CheckerId = userId;
            check.CheckObjectId = req.ObjectId;

            var hazardCount = CheckHazard.Set.Count(t => t.CheckLogId == req.CheckLogId);
            check.HazardCount = hazardCount;
            check.RemainHazardCount = hazardCount;
            check.Result = hazardCount == 0;
            check.IsClosed = false;
            check.Save();

            var obj = CheckObject.Get(req.ObjectId);
            if (obj != null)
            {
                obj.IsChecked = true;
                obj.LatestCheckDt = checkDt;
                obj.HasHarzard = hazardCount > 0;
                obj.Save();
            }

            return BuildResult(0, "保存成功");
        }

        public class CheckFlowData
        {
            public long ObjectId { get; set; }
            public string ObjectName { get; set; }
            public string DutyOrgName { get; set; }
            public string Address { get; set; }
            public string RiskLevelName { get; set; }
            public string ScopeName { get; set; }
            public string TagNames { get; set; }

            public long CheckLogId { get; set; }
            public DateTime CheckDt { get; set; }
            public long? TaskId { get; set; }

            public string PatrolOrgName { get; set; }
            public string PatrolOrgLevel { get; set; }

            public List<long> SheetIds { get; set; } = new();
            public List<CheckFlowItemRow> CheckItems { get; set; } = new();
        }

        public class CheckFlowItemRow
        {
            public long CheckItemId { get; set; }
            public long SheetId { get; set; }
            public string SheetName { get; set; }
            public string Name { get; set; }
            public string Result { get; set; }
            public string Remark { get; set; }
            public string HazardLevelName { get; set; }
        }

        private static List<long> ReadLongList(JsonElement el)
        {
            var list = new List<long>();
            if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt64(out var num))
                        list.Add(num);
                    else if (item.ValueKind == JsonValueKind.String && long.TryParse(item.ToString(), out var parsed))
                        list.Add(parsed);
                }
            }
            else if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n))
            {
                list.Add(n);
            }
            else if (el.ValueKind == JsonValueKind.String)
            {
                var raw = el.ToString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    if (raw.StartsWith("[") && raw.EndsWith("]"))
                    {
                        try
                        {
                            var arr = JsonSerializer.Deserialize<List<long>>(raw);
                            if (arr != null) list.AddRange(arr);
                        }
                        catch { }
                    }
                    else if (long.TryParse(raw, out var parsed))
                    {
                        list.Add(parsed);
                    }
                }
            }
            return list;
        }
    }
}
