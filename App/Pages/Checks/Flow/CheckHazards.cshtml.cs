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
        [BindProperty(SupportsGet = true)]
        public long? ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        public CheckHazard Item { get; set; }

        public void OnGet(long? objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;

            if (ObjectId.GetValueOrDefault() > 0 && string.IsNullOrWhiteSpace(ObjectName))
            {
                ObjectName = CheckObject.Get(ObjectId.Value)?.Name ?? string.Empty;
            }
        }

        public IActionResult OnGetData(Paging pi, string objectName, long? objectId, string checkerName, long? checkerId, string status, DateTime? createStartDt, long? dutyOrgId = null, List<long> dutyOrgIds = null, DateTime? expireTo = null)
        {
            dutyOrgIds = ParseOrgIds(dutyOrgIds, Request.Query["dutyOrgIds"]);
            var rawExcludeArchived = Request.Query["excludeArchived"].FirstOrDefault();
            bool excludeArchived = bool.TryParse(rawExcludeArchived, out var ea) && ea;

            CheckHazardStatus? singleStatus = null;
            List<CheckHazardStatus> multiStatus = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                var parts = status.Split(',', StringSplitOptions.RemoveEmptyEntries);
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
                var uid = checkerId.Value;
                var u = App.DAL.User.ValidSet.AsNoTracking().FirstOrDefault(x => x.Id == uid);
                List<long> scopeOrgIds = null;
                if (u != null)
                {
                    var raw = u.AuthOrgIds ?? new List<long>();
                    if (u.OrgId.HasValue && !raw.Contains(u.OrgId.Value))
                        raw.Add(u.OrgId.Value);
                    if (raw.Count > 0)
                    {
                        scopeOrgIds = App.DAL.Org.All
                            .GetDescendants(raw.Distinct().ToList())
                            .Select(t => t.Id)
                            .Distinct()
                            .ToList();
                    }
                }
                baseQ = App.DAL.CheckHazard.IncludeSet
                    .Include(t => t.Object).Include(t => t.Checker).Include(t => t.CheckItem)
                    .AsNoTracking()
                    .Where(h => (h.CheckerId.HasValue && h.CheckerId.Value == uid)
                             || (scopeOrgIds != null && scopeOrgIds.Count > 0
                                 && h.Object != null && h.Object.DutyOrgId.HasValue
                                 && scopeOrgIds.Contains(h.Object.DutyOrgId.Value)));
                if (!string.IsNullOrWhiteSpace(objectName))
                    baseQ = baseQ.Where(o => o.Object.Name.Contains(objectName.Trim()));
                if (objectId.HasValue && objectId.Value > 0)
                    baseQ = baseQ.Where(o => o.ObjectId == objectId.Value);
                if (!string.IsNullOrWhiteSpace(checkerName))
                    baseQ = baseQ.Where(o => o.Checker.Name.Contains(checkerName.Trim()));
            }
            else
            {
                baseQ = CheckHazard.Search(objectName, objectId, checkerName, checkerId, singleStatus, createStartDt, dutyOrgId, expireTo, dutyOrgIds);
            }

            var q = baseQ;
            if (excludeArchived)
                q = q.Where(h => !h.Status.HasValue || h.Status.Value != CheckHazardStatus.Archived);
            else if (multiStatus != null && multiStatus.Count > 0)
                q = q.Where(h => h.Status.HasValue && multiStatus.Contains(h.Status.Value));
            else if (singleStatus.HasValue)
                q = q.Where(h => h.Status.HasValue && h.Status.Value == singleStatus.Value);
            if (expireTo.HasValue)
                q = q.Where(h => h.ExpireDt.HasValue && h.ExpireDt.Value <= expireTo.Value.Date.AddDays(1).AddTicks(-1));
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
            if (!CheckPower(Power.CheckHazardDelete))
                return BuildResult(403, "无权操作");
            foreach (var id in ids)
                CheckHazard.Delete(id);
            return BuildResult(0, "删除成功");
        }

        public IActionResult OnPostSave([FromBody] CheckHazard req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckHazardEdit))
                return BuildResult(403, "无权操作");

            CheckHazard item = req.Id == 0 ? new CheckHazard() : CheckHazard.Get(req.Id);
            item.Status = req.Status;
            item.ExpireDt = req.ExpireDt;
            item.RectifyDt = req.RectifyDt;
            item.IsIn141 = req.IsIn141;
            item.ObjectId = req.ObjectId;
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
