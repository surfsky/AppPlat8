using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [Auth(Power.CheckHazardView)]
    public class CheckHazardsModel : AuthModel
    {
        [BindProperty(SupportsGet = true)]    public long? ObjectId { get; set; }
        [BindProperty(SupportsGet = true)]    public string ObjectName { get; set; }
        [BindProperty(SupportsGet = true)]    public long? CheckId { get; set; }

        public long? ScopeCheckerId { get; set; }   // 检查人 Id
        public string ScopeCheckerName { get; set; } // 检查人姓名
        public string ScopeStatus { get; set; } // 状态过滤值（支持多选，逗号分隔）
        public DateTime? ScopeExpireTo { get; set; }  // 整改期限上限
        public bool ScopeExcludeArchived { get; set; }   // 是否排除已归档

        public CheckHazard Item { get; set; }

        
        public void OnGet(long? objectId, string objectName, long? checkId)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            CheckId = checkId;

            if (ObjectId.GetValueOrDefault() > 0 && string.IsNullOrWhiteSpace(ObjectName))
            {
                ObjectName = CheckObject.Get(ObjectId.Value)?.Name ?? string.Empty;
            }

            if (CheckId.GetValueOrDefault() > 0)
                return;

            // 读取 URL 注入的 scope 参数，供过滤控件回显
            var rawCheckerId = Request.Query["checkerId"].FirstOrDefault();
            if (long.TryParse(rawCheckerId, out var cid) && cid > 0)
            {
                ScopeCheckerId = cid;
                var u = App.DAL.User.Get(cid);
                ScopeCheckerName = u != null ? (u.RealName.IsNotEmpty() ? u.RealName : u.Name) : string.Empty;
            }
            else
            {
                var currentUser = GetUser();
                if (currentUser != null)
                {
                    ScopeCheckerId = currentUser.Id;
                    ScopeCheckerName = currentUser.RealName.IsNotEmpty() ? currentUser.RealName : currentUser.Name;
                }
            }

            var rawStatus = Request.Query["status"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawStatus)) ScopeStatus = rawStatus;
            var rawExpireTo = Request.Query["expireTo"].FirstOrDefault();
            if (DateTime.TryParse(rawExpireTo, out var et)) ScopeExpireTo = et.Date;
            var rawExclude = Request.Query["excludeArchived"].FirstOrDefault();
            ScopeExcludeArchived = bool.TryParse(rawExclude, out var ea) && ea;
        }

        public IActionResult OnGetData(Paging pi, string objectName, long? objectId, long? checkId, string checkerName, long? checkerId, List<string> status, DateTime? createStartDt, long? dutyOrgId = null, List<long> dutyOrgIds = null, DateTime? expireTo = null)
        {
            dutyOrgIds = ParseOrgIds(dutyOrgIds, Request.Query["dutyOrgIds"]);
            var rawExcludeArchived = Request.Query["excludeArchived"].FirstOrDefault();
            bool excludeArchived = bool.TryParse(rawExcludeArchived, out var ea) && ea;

            // 状态过滤值：兼容 ?status=0,1（单个逗号串）与多选控件提交的重复参数 status=0&status=1
            var statusText = string.Join(",", status ?? new List<string>());

            CheckHazardStatus? singleStatus = null;
            List<CheckHazardStatus> multiStatus = null;
            if (!string.IsNullOrWhiteSpace(statusText))
            {
                var parts = statusText.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var sl = new List<CheckHazardStatus>();
                foreach (var s in parts)
                {
                    if (Enum.TryParse<CheckHazardStatus>(s.Trim(), true, out var v))
                        sl.Add(v);
                }
                if (sl.Count == 1) singleStatus = sl[0];
                else if (sl.Count > 1) multiStatus = sl;
            }

            IQueryable<CheckHazard> baseQ;
            if (dutyOrgIds.Count == 0 && !dutyOrgId.HasValue && checkerId.HasValue)
            {
                // 与 WorkDesk.BuildHazardScopeQuery 对齐：scope = CheckerId == uid OR Object.DutyOrgId ∈ EffectiveAuthOrgId 的子孙
                var uid = checkerId.Value;
                var u = App.DAL.User.ValidSet.AsNoTracking().FirstOrDefault(x => x.Id == uid);
                List<long> scopeOrgIds = null;
                var rootOrgId = u?.AuthOrgId;
                if (rootOrgId.HasValue && rootOrgId.Value > 0)
                {
                    scopeOrgIds = App.DAL.Org.All
                        .GetDescendants(rootOrgId.Value)
                        .Select(t => t.Id)
                        .Distinct()
                        .ToList();
                }
                baseQ = App.DAL.CheckHazard.IncludeSet
                    .Include(t => t.CheckObject).Include(t => t.Checker).Include(t => t.CheckItem)
                    .AsNoTracking()
                    .Where(h => (h.CheckerId.HasValue && h.CheckerId.Value == uid)
                             || (scopeOrgIds != null && scopeOrgIds.Count > 0
                                 && h.CheckObject != null && h.CheckObject.DutyOrgId.HasValue
                                 && scopeOrgIds.Contains(h.CheckObject.DutyOrgId.Value)));
                if (!string.IsNullOrWhiteSpace(objectName))
                    baseQ = baseQ.Where(o => o.CheckObject.Name.Contains(objectName.Trim()));
                if (objectId.HasValue && objectId.Value > 0)
                    baseQ = baseQ.Where(o => o.CheckObjectId == objectId.Value);
                if (!string.IsNullOrWhiteSpace(checkerName))
                    baseQ = baseQ.Where(o => o.Checker.Name.Contains(checkerName.Trim()));
            }
            else
            {
                baseQ = CheckHazard.Search(objectName, objectId, checkerName, checkerId, singleStatus, createStartDt, dutyOrgId, expireTo, dutyOrgIds);
            }

            var q = baseQ;
            if (checkId.HasValue && checkId.Value > 0)
                q = q.Where(h => h.CheckLogId == checkId.Value);
            if (excludeArchived)
                q = q.Where(h => !h.Status.HasValue || h.Status.Value != CheckHazardStatus.Archived);
            else if (multiStatus != null && multiStatus.Count > 0)
                q = q.Where(h => h.Status.HasValue && multiStatus.Contains(h.Status.Value));
            else if (singleStatus.HasValue)
                q = q.Where(h => h.Status.HasValue && h.Status.Value == singleStatus.Value);
            // 与 WorkDesk 超期统计对齐：ExpireDt <= 当日零点（DateTime.Today），而非当日结束
            if (expireTo.HasValue)
                q = q.Where(h => h.ExpireDt.HasValue && h.ExpireDt.Value <= expireTo.Value.Date);
            if (createStartDt.HasValue)
                q = q.Where(h => h.CreateDt >= createStartDt.Value.Date);

            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        static List<long> ParseOrgIds(List<long> fromBinder, Microsoft.Extensions.Primitives.StringValues raw)
        {
            var result = new List<long>();
            if (fromBinder != null && fromBinder.Count > 0) result.AddRange(fromBinder);
            var combined = raw.ToString();
            if (!string.IsNullOrWhiteSpace(combined))
            {
                foreach (var part in combined.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (long.TryParse(part.Trim(), out var id) && id > 0)
                        result.Add(id);
                }
            }
            return result.Distinct().ToList();
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!HasPower(Power.CheckHazardDelete))
                return BuildResult(403, "无权操作");
            foreach (var id in ids)
                CheckHazard.Delete(id);
            return BuildResult(0, "删除成功");
        }

        public IActionResult OnPostSave([FromBody] CheckHazard req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (!HasPower(Power.CheckHazardEdit))
                return BuildResult(403, "无权操作");

            CheckHazard item = req.Id == 0 ? new CheckHazard() : CheckHazard.Get(req.Id);
            item.Status = req.Status;
            item.ExpireDt = req.ExpireDt;
            item.RectifyDt = req.RectifyDt;
            item.IsIn141 = req.IsIn141;
            item.CheckObjectId = req.CheckObjectId;
            item.CheckerId = req.CheckerId;
            item.CheckLogId = req.CheckLogId;
            item.CheckSheetId = req.CheckSheetId;
            item.CheckItemId = req.CheckItemId;
            item.CheckItemText = req.CheckItemText;
            item.Description = req.Description;
            item.Save();
                
            return BuildResult(0, "保存成功");
        }
    }
}
