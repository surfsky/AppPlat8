using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Me
{
    public class WorkDeskOrgModel : AdminModel
    {
        //---------------------------------------------------------------------
        // 筛选条件（来自 URL 查询参数）
        //---------------------------------------------------------------------
        [BindProperty(SupportsGet = true)] public long? OrgId { get; set; }
        [BindProperty(SupportsGet = true)] public string OrgName { get; set; }

        public string OrgDisplay { get; private set; }

        /// <summary>能否切换组织（管理员/领导 才可以）</summary>
        public bool CanChangeScope => Auth.CheckPower(Power.UserView) || Auth.CheckPower(Power.OrgView);

        // 责任部门（默认当前用户所属部门；管理员可覆盖）
        public long EffectiveOrgId
        {
            get
            {
                if (CanChangeScope && OrgId.HasValue && OrgId.Value > 0)
                    return OrgId.Value;
                var selfId = GetUserId();
                if (selfId.HasValue && selfId.Value > 0)
                {
                    var u = App.DAL.User.Get(selfId.Value);
                    if (u != null && u.OrgId.HasValue && u.OrgId.Value > 0)
                        return u.OrgId.Value;
                }
                return 0L;
            }
        }

        // EffectiveOrgId 及其所有子孙
        public List<long> EffectiveOrgIds => OrgDescendants(EffectiveOrgId);

        //---------------------------------------------------------------------
        // 角标计数（对象 4 卡，隐患 3 卡）
        //---------------------------------------------------------------------
        public int CountOrgObjects         { get; set; }
        public int CountUncheckedObjects   { get; set; }
        public int CountNearExpireObjects  { get; set; }
        public int CountOverdueObjects     { get; set; }
        public int CountOrgHazards         { get; set; }
        public int CountPendingHazards     { get; set; }
        public int CountOverdueHazards     { get; set; }

        public List<WorkDeskStatCard> ObjectStatCards { get; set; } = new List<WorkDeskStatCard>();
        public List<WorkDeskStatCard> HazardStatCards { get; set; } = new List<WorkDeskStatCard>();

        //---------------------------------------------------------------------
        // 页面加载
        //---------------------------------------------------------------------
        public void OnGet()
        {
            var rawOrgId = Request.Query["orgId"].FirstOrDefault();
            if (long.TryParse(rawOrgId, out var oidQ) && oidQ > 0) OrgId = oidQ;

            ResolveOrgDisplay();
            var today = DateTime.Today;
            var near  = today.AddDays(7);

            var objects = BuildObjectScopeQuery();
            CountOrgObjects = objects.Count();
            var projection = objects
                .Select(o => new {
                    o.IsChecked,
                    o.LastCheckDt,
                    o.RiskLevel
                })
                .AsNoTracking()
                .ToList();

            CountUncheckedObjects = projection
                .Count(o => (o.IsChecked == null || o.IsChecked == false) || o.LastCheckDt == null);
            CountNearExpireObjects = projection
                .Count(o => {
                    var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                    return next.HasValue && next.Value > today && next.Value <= near;
                });
            CountOverdueObjects = projection
                .Count(o => {
                    var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                    return next.HasValue && next.Value <= today;
                });

            ObjectStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("科室对象",           CountOrgObjects,        AppendObjectScope($"/Checks/CheckObjects?isDel=false")),
                new WorkDeskStatCard("未巡查对象",         CountUncheckedObjects,  AppendObjectScope($"/Checks/CheckObjects?isDel=false&isChecked=false")),
                new WorkDeskStatCard("临期巡查对象",       CountNearExpireObjects, AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck={today.AddDays(1):yyyy-MM-dd},{today.AddDays(7):yyyy-MM-dd}")),
                new WorkDeskStatCard("超期未巡查对象",     CountOverdueObjects,    AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck=,{today:yyyy-MM-dd}")),
            };

            var hazards = BuildHazardScopeQuery();
            CountOrgHazards     = hazards.Count();
            CountPendingHazards = hazards.Where(h => h.Status == CheckHazardStatus.Waiting || h.Status == CheckHazardStatus.Processing).Count();
            CountOverdueHazards = hazards.Where(h => h.Status != CheckHazardStatus.Archived && h.ExpireDt.HasValue && h.ExpireDt.Value <= today).Count();
            HazardStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("发现的隐患",  CountOrgHazards,     AppendHazardScope($"/Checks/CheckHazards")),
                new WorkDeskStatCard("待处理隐患",  CountPendingHazards, AppendHazardScope($"/Checks/CheckHazards?status=0,1")),
                new WorkDeskStatCard("超期隐患",    CountOverdueHazards, AppendHazardScope($"/Checks/CheckHazards?excludeArchived=true&expireTo={today:yyyy-MM-dd}")),
            };
        }

        //---------------------------------------------------------------------
        // 表格：科室任务
        //---------------------------------------------------------------------
        public IActionResult OnGetOrgTasks(Paging pi, string taskTab)
        {
            var rawOrgId = Request.Query["orgId"].FirstOrDefault();
            if (long.TryParse(rawOrgId, out var oidQ) && oidQ > 0) OrgId = oidQ;
            var rawTaskTab = Request.Query["taskTab"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawTaskTab)) taskTab = rawTaskTab;

            var orgIds = EffectiveOrgIds;
            var validUsers = App.DAL.User.ValidSet.AsNoTracking()
                .Where(u => u.OrgId.HasValue)
                .Select(u => new { u.Id, u.OrgId })
                .AsEnumerable()
                .Where(u => orgIds.Contains(u.OrgId!.Value))
                .Select(u => u.Id)
                .Distinct()
                .ToList();

            var taskOrgIds = orgIds.Count == 0
                ? new List<long>()
                : App.DAL.CheckTaskOrg.ValidSet.AsNoTracking()
                    .Where(to => to.OrgId != null && orgIds.Contains(to.OrgId.Value) && to.TaskId != null)
                    .Select(to => to.TaskId.Value)
                    .Distinct()
                    .ToList();

            IQueryable<CheckTask> BaseQry()
                => App.DAL.CheckTask.Search(null, null, null)
                    .AsNoTracking()
                    .Include(t => t.Creator).ThenInclude(u => u.Org)
                    .Include(t => t.Orgs).ThenInclude(o => o.Org)
                    .Where(t => (validUsers.Count > 0 && t.CreatorId.HasValue && validUsers.Contains(t.CreatorId.Value))
                             || (taskOrgIds.Count > 0 && taskOrgIds.Contains(t.Id)));

            var q = BaseQry();
            var tab = (taskTab ?? string.Empty).ToLower();
            switch (tab)
            {
                case "created":
                {
                    q = validUsers.Count == 0
                        ? q.Where(t => false)
                        : q.Where(t => t.CreatorId.HasValue && t.CreatorId.Value > 0 && validUsers.Contains(t.CreatorId.Value));
                }
                break;
                case "handled":
                    q = taskOrgIds.Count == 0
                        ? q.Where(t => false)
                        : q.Where(t => taskOrgIds.Contains(t.Id));
                    break;
                case "all":
                    break;
                case "unfinished":
                default:
                    q = q.Where(t => !((t.TotalCount ?? 0) > 0 && (t.FinishCount ?? 0) >= (t.TotalCount ?? 0)));
                    break;
            }
            return BuildResult(0, "success", MaterializeAndProject(q, pi), pi);
        }

        //---------------------------------------------------------------------
        // Helpers（同 WorkDeskModel）
        //---------------------------------------------------------------------
        private void ResolveOrgDisplay()
        {
            if (OrgId.HasValue && OrgId.Value > 0)
            {
                var org = OrgGet(OrgId.Value);
                if (org != null) { OrgDisplay = org.Name; return; }
            }
            if (!string.IsNullOrWhiteSpace(OrgName))
            {
                var key = OrgName.Trim();
                var org = OrgAll().FirstOrDefault(x => x.Name == key || x.FullName == key);
                if (org != null) { OrgDisplay = org.Name; OrgId = org.Id; return; }
            }
            var oid = EffectiveOrgId;
            if (oid > 0)
            {
                var org = OrgGet(oid);
                if (org != null) { OrgDisplay = org.Name; if (!OrgId.HasValue || OrgId.Value <= 0) OrgId = oid; return; }
            }
            OrgDisplay = "请选择组织";
        }

        private static DateTime? ComputeNextCheckDt(DateTime? latestCheckDt, CheckRiskLevel? riskLevel)
        {
            if (!latestCheckDt.HasValue) return null;
            int months = GetCheckCycleMonths(riskLevel);
            return latestCheckDt.Value.AddMonths(months);
        }
        private static int GetCheckCycleMonths(CheckRiskLevel? riskLevel)
        {
            return riskLevel switch
            {
                CheckRiskLevel.None   => 12,
                CheckRiskLevel.Low    => 9,
                CheckRiskLevel.Medium => 6,
                CheckRiskLevel.High   => 3,
                _ => 12,
            };
        }

        private static List<object> MaterializeAndProject(IQueryable<CheckTask> query, Paging pi)
        {
            if (pi == null) pi = new Paging();
            if (pi.SortField.IsEmpty()) { pi.SortField = "Id"; pi.SortDirection = "DESC"; }
            pi.SetTotal(query.Count());
            var sorted = query.SortBy(pi.SortField + " " + pi.SortDirection).AsQueryable();
            var page = sorted.SortAndPage(pi).ToList();

            return page
                .Select(t => (object)new WorkDeskTaskRow
                {
                    Id           = t.Id,
                    Name         = t.Name,
                    LevelText    = t.Orgs != null && t.Orgs.Count > 0
                                 ? string.Join("/", t.Orgs.Select(o => o.Org?.FullName ?? o.Org?.Name ?? "").Take(3))
                                 : "",
                    CreatorText  = t.CreatorName,
                    CreatorOrg   = t.Creator?.Org?.Name ?? "",
                    Progress     = (int)(t.Progress ?? 0),
                    ProgressText = ComputeProgressText(t),
                    StartDt      = t.StartDt,
                    ExpireDt     = t.ExpireDt,
                    DetailUrl    = $"/Checks/CheckTaskObjects?taskId={t.Id}",
                })
                .ToList();
        }
        private static string ComputeProgressText(CheckTask t)
        {
            if (t.ExpireDt.HasValue && t.ExpireDt.Value <= DateTime.Now) return "超期";
            if (t.TotalCount > 0 && t.FinishCount >= t.TotalCount)      return "已完成";
            return "进行中";
        }

        private IQueryable<CheckObject> BuildObjectScopeQuery()
        {
            var orgIds = EffectiveOrgIds;
            return App.DAL.CheckObject.Search(isDel: false)
                .Where(o => orgIds.Count > 0 && o.DutyOrgId.HasValue && orgIds.Contains(o.DutyOrgId.Value));
        }

        private IQueryable<CheckHazard> BuildHazardScopeQuery()
        {
            var orgIds = EffectiveOrgIds;
            return App.DAL.CheckHazard.Search(null, null, null, null, null, null)
                .Where(h => orgIds.Count > 0
                         && h.Object != null
                         && h.Object.DutyOrgId.HasValue
                         && orgIds.Contains(h.Object.DutyOrgId.Value));
        }

        //---------------------------------------------------------------------
        // 卡片 URL 拼接：scope 默认按当前组织（dutyOrgId 单值）
        //---------------------------------------------------------------------
        private string AppendObjectScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl ?? string.Empty;
            var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);

            if (EffectiveOrgId > 0) qs["dutyOrgId"] = EffectiveOrgId.ToString();

            if (qs.Count == 0) return rawUrl;
            var sep = rawUrl.Contains('?') ? '&' : '?';
            return rawUrl + sep + qs.ToString();
        }

        private string AppendHazardScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl ?? string.Empty;
            var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);

            if (EffectiveOrgId > 0) qs["dutyOrgId"] = EffectiveOrgId.ToString();

            if (qs.Count == 0) return rawUrl;
            var sep = rawUrl.Contains('?') ? '&' : '?';
            return rawUrl + sep + qs.ToString();
        }

        // 兼容封装（避免与同命名空间 DAL.Org 冲突）
        private static List<App.DAL.Org> OrgAll()                       => App.DAL.Org.All;
        private static App.DAL.Org        OrgGet(long id)               => App.DAL.Org.Get(id);
        private static List<long> OrgDescendants(long rootOrgId)
        {
            if (rootOrgId <= 0) return new List<long>();
            return OrgAll()
                .GetDescendants(rootOrgId)
                .Cast<App.DAL.Org>()
                .Select(o => o.Id)
                .Distinct()
                .ToList();
        }
    }
}
