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
                new WorkDeskStatCard("科室对象",           CountOrgObjects,        "/Checks/CheckObjects"),
                new WorkDeskStatCard("未巡查对象",         CountUncheckedObjects,  "/Checks/CheckObjects?isChecked=false"),
                new WorkDeskStatCard("临期巡查对象",       CountNearExpireObjects, "/Checks/CheckObjects"),
                new WorkDeskStatCard("超期未巡查对象",     CountOverdueObjects,    "/Checks/CheckObjects"),
            };

            var hazards = BuildHazardScopeQuery();
            CountOrgHazards     = hazards.Count();
            CountPendingHazards = hazards.Where(h => h.Status == CheckHazardStatus.Pending || h.Status == CheckHazardStatus.Rectifying).Count();
            CountOverdueHazards = hazards.Where(h => h.Status != CheckHazardStatus.Closed && h.ExpireDt.HasValue && h.ExpireDt.Value <= today).Count();
            HazardStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("发现的隐患",  CountOrgHazards,     "/Checks/CheckHazards"),
                new WorkDeskStatCard("待处理隐患",  CountPendingHazards, "/Checks/CheckHazards?status=0"),
                new WorkDeskStatCard("超期隐患",    CountOverdueHazards,"/Checks/CheckHazards"),
            };
        }

        //---------------------------------------------------------------------
        // 表格：科室任务
        //---------------------------------------------------------------------
        public IActionResult OnGetOrgTasks(Paging pi, string taskTab)
        {
            var orgIds = EffectiveOrgIds;
            var taskIds = orgIds.Count == 0
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
                    .Where(t => taskIds.Contains(t.Id));

            var q = BaseQry();
            var tab = (taskTab ?? string.Empty).ToLower();
            switch (tab)
            {
                case "created":
                {
                    // 科室创建的：创建者的 org 在当前科室 org 范围内
                    var validUsers = App.DAL.User.ValidSet.AsNoTracking()
                        .Where(u => u.OrgId.HasValue)
                        .Select(u => new { u.Id, u.OrgId })
                        .AsEnumerable()
                        .Where(u => orgIds.Contains(u.OrgId!.Value))
                        .Select(u => u.Id)
                        .Distinct()
                        .ToList();
                    q = validUsers.Count == 0
                        ? q.Where(t => false)
                        : q.Where(t => t.CreatorId.HasValue && t.CreatorId.Value > 0 && validUsers.Contains(t.CreatorId.Value));
                }
                break;
                case "handled":
                    // 科室经手 = 任务分派到科室 org（默认 q = 这个）
                    break;
                case "all":
                    break;
                case "unfinished":
                default:
                    q = q.Where(t => !(t.TotalCount > 0 && t.FinishCount >= t.TotalCount));
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
