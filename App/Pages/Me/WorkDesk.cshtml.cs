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
    public class WorkDeskModel : BaseModel
    {
        //---------------------------------------------------------------------
        // 筛选条件（来自 URL 查询参数；页面顶部人员 picker 绑定到这些）
        //---------------------------------------------------------------------
        [BindProperty(SupportsGet = true)] public long? OrgId { get; set; }
        [BindProperty(SupportsGet = true)] public long? UserId { get; set; }
        [BindProperty(SupportsGet = true)] public string UserName { get; set; }

        /// <summary>根据 UserId 反查的真实姓名（SSR 回显 / 初始值）。</summary>
        public string UserRealName { get; private set; }

        // 管理员（有用户查看权限）可切换到其它中心/人员视图，非管理员只能看自己
        public bool CanChangeScope => Auth.CheckPower(Power.UserView);

        // 实际要查询的目标用户：URL(管理员) > 当前登录用户
        public long EffectiveUserId
        {
            get
            {
                if (CanChangeScope && UserId.HasValue && UserId.Value > 0)
                    return UserId.Value;
                var uid = GetUserId();
                return uid ?? 0L;
            }
        }

        // 实际责任网格命中范围：URL(管理员) > 用户 OrgId + AuthOrgIds 展开后的全部子孙
        public List<long> EffectiveOrgIds => ResolveEffectiveOrgIds();

        // 责任网格根节点（未展开）：用于 URL 拼接，避免 Query 超长
        public List<long> EffectiveRootOrgIds => ResolveEffectiveRootOrgIds();

        //---------------------------------------------------------------------
        // 角标计数（对象 4 卡，隐患 3 卡）
        //---------------------------------------------------------------------
        public int CountMyObjects            { get; set; }
        public int CountUncheckedObjects     { get; set; }
        public int CountNearExpireObjects    { get; set; }
        public int CountOverdueObjects       { get; set; }
        public int CountMyHazards            { get; set; }
        public int CountPendingHazards       { get; set; }
        public int CountOverdueHazards       { get; set; }

        //---------------------------------------------------------------------
        // 简单绑定用列表（SSR 输出）
        //---------------------------------------------------------------------
        // 已废弃：快捷入口配置已移到 WorkDesk.cshtml 顶部代码块中（List<WorkDeskLink> quickEntries），
        // 调整标题/链接/打开方式直接改 .cshtml 即可，无需重新编译 PageModel。
        [Obsolete("快捷入口配置已移到 WorkDesk.cshtml 顶部，此字段保留为空占位")]
        public List<WorkDeskLink> QuickEntries { get; } = new();
        public List<WorkDeskStatCard> ObjectStatCards { get; set; } = new List<WorkDeskStatCard>();
        public List<WorkDeskStatCard> HazardStatCards { get; set; } = new List<WorkDeskStatCard>();

        //---------------------------------------------------------------------
        // 页面加载：统计计数 + 卡片初始化
        //---------------------------------------------------------------------
        public void OnGet()
        {
            // 任务 handler / EleTable AJAX 通过 Query 传参，这里也优先读 Query，
            // 避免 BindProperty 只在 SSR 首屏生效（切 tab / 切 picker 后 AJAX 请求不带 BindProperty 值）
            var rawUserId = Request.Query["userId"].FirstOrDefault();
            if (long.TryParse(rawUserId, out var uidQ) && uidQ > 0) UserId = uidQ;

            ResolveUserRealName();
            var today = DateTime.Today;
            var near  = today.AddDays(7);

            var objects = BuildObjectScopeQuery();
            CountMyObjects = objects.Count();

            // NextCheckDt 是实体 getter-only 计算属性（NotMapped），EF Core 无法翻译为 SQL。
            // 解决办法：先把 LatestCheckDt / RiskLevel / IsChecked 投影到内存，再本地计数。
            var projection = objects
                .Select(o => new
                {
                    o.IsChecked,
                    o.LastCheckDt,
                    o.RiskLevel
                })
                .AsNoTracking()
                .ToList();
            CountUncheckedObjects = projection
                .Count(o => (o.IsChecked == null || o.IsChecked == false) || o.LastCheckDt == null);
            CountNearExpireObjects = projection
                .Count(o =>
                {
                    var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                    return next.HasValue && next.Value > today && next.Value <= near;
                });
            CountOverdueObjects = projection
                .Count(o =>
                {
                    var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                    return next.HasValue && next.Value <= today;
                });

            ObjectStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("我的对象",         CountMyObjects,         AppendObjectScope($"/Checks/CheckObjects?isDel=false")),
                new WorkDeskStatCard("未巡查对象",       CountUncheckedObjects,  AppendObjectScope($"/Checks/CheckObjects?isDel=false&isChecked=false")),
                new WorkDeskStatCard("临期巡查对象",     CountNearExpireObjects, AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck={today.AddDays(1):yyyy-MM-dd},{today.AddDays(7):yyyy-MM-dd}")),
                new WorkDeskStatCard("超期未巡查对象",   CountOverdueObjects,    AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck=,{today:yyyy-MM-dd}")),
            };

            var hazards = BuildHazardScopeQuery();
            CountMyHazards       = hazards.Count();
            CountPendingHazards  = hazards.Where(h => h.Status == CheckHazardStatus.Waiting || h.Status == CheckHazardStatus.Processing).Count();
            CountOverdueHazards  = hazards.Where(h => h.Status != CheckHazardStatus.Archived && h.ExpireDt.HasValue && h.ExpireDt.Value <= today).Count();
            HazardStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("我发现的隐患",   CountMyHazards,      AppendHazardScope($"/Checks/CheckHazards")),
                new WorkDeskStatCard("待处理隐患",     CountPendingHazards, AppendHazardScope($"/Checks/CheckHazards?status=0,1")),
                new WorkDeskStatCard("超期隐患",       CountOverdueHazards, AppendHazardScope($"/Checks/CheckHazards?excludeArchived=true&expireTo={today:yyyy-MM-dd}")),
            };
        }

        //---------------------------------------------------------------------
        // Helpers：计算下一次巡查时间（与 CheckObject.NextCheckDt 实现保持一致）
        //---------------------------------------------------------------------
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
                _ => 12
            };
        }

        //---------------------------------------------------------------------
        // 表格 1：我的任务（我创建 OR 分派到我命中的责任网格）+ taskTab 筛选
        //   taskTab=unfinished(默认)：排除已完成，其余全部（进行中+超期）
        //   taskTab=all：全部
        //   taskTab=created：我发起（CreatorId == uid）
        //   taskTab=handled：我经手（CreatorId == uid 或 分配到我的 org）
        //---------------------------------------------------------------------
        public IActionResult OnGetMyTasks(Paging pi, string taskTab)
        {
            // 先保证能把任务数据展示出来：忽略任何 tab / scope，取全部任务，按 Id 倒序。
            // 后续迭代再逐步加上 created/handled/unfinished 的 tab 过滤。
            var q = App.DAL.CheckTask.Search(null, null, null)
                .AsNoTracking()
                .Include(t => t.Creator).ThenInclude(u => u.Org)
                .Include(t => t.Orgs).ThenInclude(o => o.Org);
            return BuildResult(0, "success", MaterializeAndProject(q, pi), pi);
        }

        //---------------------------------------------------------------------
        // 表格 2：基础科任务（当前目标用户所在科室级，含下属）
        //---------------------------------------------------------------------
        public IActionResult OnGetSectionTasks(Paging pi)
        {
            var sectionIds = GetSectionScopeOrgIds();
            var taskIds = sectionIds.Count == 0
                ? new List<long>()
                : App.DAL.CheckTaskOrg.ValidSet.AsNoTracking()
                    .Where(to => to.OrgId != null && sectionIds.Contains(to.OrgId.Value) && to.TaskId != null)
                    .Select(to => to.TaskId.Value)
                    .Distinct()
                    .ToList();

            var q = App.DAL.CheckTask.Search(null, null, null)
                .AsNoTracking()
                .Include(t => t.Creator).ThenInclude(u => u.Org)
                .Include(t => t.Orgs).ThenInclude(o => o.Org)
                .Where(t => taskIds.Contains(t.Id));
            return BuildResult(0, "success", MaterializeAndProject(q, pi), pi);
        }

        //---------------------------------------------------------------------
        // 表格 3：分派中心任务（当前目标用户所在单位级，含下属；若枚举没 Center，退回到 Unit）
        //---------------------------------------------------------------------
        public IActionResult OnGetCenterTasks(Paging pi)
        {
            var centerIds = GetCenterScopeOrgIds();
            var taskIds = centerIds.Count == 0
                ? new List<long>()
                : App.DAL.CheckTaskOrg.ValidSet.AsNoTracking()
                    .Where(to => to.OrgId != null && centerIds.Contains(to.OrgId.Value) && to.TaskId != null)
                    .Select(to => to.TaskId.Value)
                    .Distinct()
                    .ToList();

            var q = App.DAL.CheckTask.Search(null, null, null)
                .AsNoTracking()
                .Include(t => t.Creator).ThenInclude(u => u.Org)
                .Include(t => t.Orgs).ThenInclude(o => o.Org)
                .Where(t => taskIds.Contains(t.Id));
            return BuildResult(0, "success", MaterializeAndProject(q, pi), pi);
        }

        //---------------------------------------------------------------------
        // 内部：EF 查询先 ToList（避免 EF 翻译本地函数/字符串拼接失败），再手动 SortPage 成 WorkDeskTaskRow
        //---------------------------------------------------------------------
        private static List<object> MaterializeAndProject(IQueryable<CheckTask> query, Paging pi)
        {
            // 1) 排序前统一默认值
            if (pi == null) pi = new Paging();
            if (pi.SortField.IsEmpty()) { pi.SortField = "Id"; pi.SortDirection = "DESC"; }

            // 2) Total + 排序 + 分页
            var total = query.Count();
            pi.SetTotal(total);
            var page = query.SortAndPage(pi).ToList();

            // 3) 内存投影为 WorkDeskTaskRow → object 输出（前台列是扁平的 Prop）
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

        //---------------------------------------------------------------------
        // 内部：Scope 解析
        //---------------------------------------------------------------------

        /// <summary>根据 UserId/UserName 反查目标用户的真实姓名，SSR 回显人员 picker 显示名。</summary>
        private void ResolveUserRealName()
        {
            if (UserId.HasValue && UserId.Value > 0)
            {
                var u = App.DAL.User.Get(UserId.Value);
                if (u != null)
                {
                    UserRealName = (u.RealName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(UserRealName))
                        UserRealName = (u.Name ?? string.Empty).Trim();
                    return;
                }
            }
            if (!string.IsNullOrWhiteSpace(UserName))
            {
                var key = UserName.Trim();
                var u = App.DAL.User.Set.FirstOrDefault(x => x.Name == key || x.RealName == key);
                if (u != null)
                {
                    UserRealName = (u.RealName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(UserRealName))
                        UserRealName = (u.Name ?? string.Empty).Trim();
                    if (!UserId.HasValue || UserId.Value <= 0)
                        UserId = u.Id;
                    return;
                }
            }
            var selfId = GetUserId();
            if (selfId.HasValue && selfId.Value > 0)
            {
                var u = App.DAL.User.Get(selfId.Value);
                if (u != null)
                {
                    UserRealName = (u.RealName ?? string.Empty).Trim();
                    if (string.IsNullOrWhiteSpace(UserRealName))
                        UserRealName = (u.Name ?? string.Empty).Trim();
                    if (!UserId.HasValue || UserId.Value <= 0)
                        UserId = u.Id;
                }
            }
        }

        private List<long> ResolveEffectiveOrgIds()
        {
            var ids = ResolveEffectiveRootOrgIds();
            if (ids.Count == 0) return new List<long>();
            return OrgDescendants(ids);
        }

        private List<long> ResolveEffectiveRootOrgIds()
        {
            if (CanChangeScope && OrgId.HasValue && OrgId.Value > 0)
                return new List<long> { OrgId.Value };

            var user = App.DAL.User.Get(EffectiveUserId);
            if (user == null) return new List<long>();

            var ids = new List<long>();
            if (user.OrgId.HasValue) ids.Add(user.OrgId.Value);
            if (user.AuthOrgIds != null) ids.AddRange(user.AuthOrgIds);
            return ids.Distinct().Where(x => x > 0).ToList();
        }

        private List<long> GetSectionScopeOrgIds()
        {
            var user    = App.DAL.User.Get(EffectiveUserId);
            var org     = user?.OrgId.HasValue == true ? OrgGet(user.OrgId.Value) : null;
            var section = org?.GetAncestor(OrgLevel.Section) ?? org;
            return section == null ? new List<long>() : OrgDescendants(section.Id);
        }

        private List<long> GetCenterScopeOrgIds()
        {
            var user   = App.DAL.User.Get(EffectiveUserId);
            var org    = user?.OrgId.HasValue == true ? OrgGet(user.OrgId.Value) : null;
            var center = org?.GetAncestor(OrgLevel.Unit)
                      ?? org?.GetAncestor(OrgLevel.District)
                      ?? org;
            return center == null ? new List<long>() : OrgDescendants(center.Id);
        }

        //---------------------------------------------------------------------
        // 内部：对象 / 隐患 范围
        //---------------------------------------------------------------------
        private IQueryable<CheckObject> BuildObjectScopeQuery()
        {
            var uid    = EffectiveUserId;
            var orgIds = EffectiveOrgIds;
            return App.DAL.CheckObject.Search(isDel: false)
                .Where(o => (orgIds.Count > 0 && o.DutyOrgId.HasValue && orgIds.Contains(o.DutyOrgId.Value))
                         || (o.CheckerId == uid));
        }

        private IQueryable<CheckHazard> BuildHazardScopeQuery()
        {
            var uid    = EffectiveUserId;
            var orgIds = EffectiveOrgIds;
            return App.DAL.CheckHazard.Search(null, null, null, null, null, null)
                .Where(h => h.CheckerId == uid
                         || (orgIds.Count > 0
                             && h.Object != null
                             && h.Object.DutyOrgId.HasValue
                             && orgIds.Contains(h.Object.DutyOrgId.Value)));
        }

        //---------------------------------------------------------------------
        // 内部：CheckTask → 前台表格行（SortPageExport 支持 Func 投影，避免 EF 翻译本地函数）
        //---------------------------------------------------------------------
        private static object BuildTaskRow(CheckTask t)
        {
            return new WorkDeskTaskRow
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
            };
        }

        private static string ComputeProgressText(CheckTask t)
        {
            if (t.ExpireDt.HasValue && t.ExpireDt.Value <= DateTime.Now) return "超期";
            if (t.TotalCount > 0 && t.FinishCount >= t.TotalCount)      return "已完成";
            return "进行中";
        }

        //---------------------------------------------------------------------
        // 卡片 URL 拼接：把当前 scope（checkerId/dutyOrgId）追加到目标列表页 URL 查询参数上
        //---------------------------------------------------------------------
        private string AppendObjectScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl ?? string.Empty;
            var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);

            var roots = EffectiveRootOrgIds;
            if (roots != null && roots.Count == 1)
                qs["dutyOrgId"] = roots[0].ToString();
            else if (roots != null && roots.Count > 1)
                qs["dutyOrgIds"] = string.Join(",", roots);

            if (qs.Count == 0) return rawUrl;
            var sep = rawUrl.Contains('?') ? '&' : '?';
            return rawUrl + sep + qs.ToString();
        }

        private string AppendHazardScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl ?? string.Empty;
            var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);

            var uid = EffectiveUserId;
            if (uid > 0) qs["checkerId"] = uid.ToString();

            if (qs.Count == 0) return rawUrl;
            var sep = rawUrl.Contains('?') ? '&' : '?';
            return rawUrl + sep + qs.ToString();
        }

        //---------------------------------------------------------------------
        // 内部：兼容封装（避免 WorkDesk.cs 中同名冲突，以及和命名空间 App.Pages.Me.Org 冲突，统一用完整类型别名）
        //---------------------------------------------------------------------
        private static List<App.DAL.Org> OrgAll()                         => App.DAL.Org.All;
        private static App.DAL.Org        OrgGet(long id)                 => App.DAL.Org.Get(id);
        private static List<long> OrgDescendants(long rootOrgId)
        {
            return OrgAll()
                .GetDescendants(rootOrgId)
                .Cast<App.DAL.Org>()
                .Select(o => o.Id)
                .Distinct()
                .ToList();
        }
        private static List<long> OrgDescendants(List<long> rootOrgIds)
        {
            return OrgAll()
                .GetDescendants(rootOrgIds)
                .Cast<App.DAL.Org>()
                .Select(o => o.Id)
                .Distinct()
                .ToList();
        }
    }

    //-------------------------------------------------------------------------
    // 页面级 DTO
    //-------------------------------------------------------------------------
    public class WorkDeskLink
    {
        public string Title  { get; set; }
        public string Url    { get; set; }
        public string Target { get; set; } = "_self";
    }

    public class WorkDeskStatCard
    {
        public WorkDeskStatCard() { }
        public WorkDeskStatCard(string title, int count, string url)
        {
            Title = title; Count = count; Url = url;
        }
        public string Title { get; set; }
        public int    Count { get; set; }
        public string Url   { get; set; }
        public bool   ShowBadge => Count > 0;
    }

    public class WorkDeskTaskRow : IExport
    {
        public long      Id           { get; set; }
        public string    Name         { get; set; }
        public string    LevelText    { get; set; }
        public string    CreatorText  { get; set; }
        public string    CreatorOrg   { get; set; }
        public int       Progress     { get; set; }
        public string    ProgressText { get; set; }
        public DateTime? StartDt      { get; set; }
        public DateTime? ExpireDt     { get; set; }
        public string    DetailUrl    { get; set; }

        public object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id, Name, LevelText,
                CreatorText, CreatorOrg,
                Progress, ProgressText,
                StartDt, ExpireDt,
                DetailUrl,
            };
        }
    }
}
