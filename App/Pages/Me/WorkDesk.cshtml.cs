using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Me
{
    //=========================================================================
    // 页面 DTO
    //=========================================================================
    /// <summary>工作台链接</summary>
    public class WorkDeskLink
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Target { get; set; } = "_self";
        public string IconCls { get; set; }
    }

    /// <summary>工作台统计卡片</summary>
    public class WorkDeskStatCard
    {
        public string Title { get; set; }
        public int Count { get; set; }
        public string Url { get; set; }
        public bool ShowBadge => Count > 0;
        public string IconCls { get; set; }

        public WorkDeskStatCard() {}
        public WorkDeskStatCard(string title, int count, string url, string iconCls)
        {
            Title = title;
            Count = count;
            Url = url;
            IconCls = iconCls;
        }
    }

    /// <summary>工作台任务行</summary>
    public class WorkDeskTaskRow : IExport
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string LevelText { get; set; }
        public string CreatorText { get; set; }
        public string CreatorOrg { get; set; }
        public int Progress { get; set; }
        public string ProgressText { get; set; }
        public DateTime? StartDt { get; set; }
        public DateTime? ExpireDt { get; set; }
        public string DetailUrl { get; set; }
        public long? CheckerId { get; set; }

        public object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id, Name, LevelText,
                CreatorText, CreatorOrg,
                Progress, ProgressText,
                StartDt, ExpireDt,
                DetailUrl, CheckerId,
            };
        }
    }

    
    //=========================================================================
    // 页面 Model
    //=========================================================================
    public class WorkDeskModel : AuthModel
    {
        //---------------------------------------------------------------------
        // 筛选条件（来自 URL 查询参数；页面顶部人员 picker 绑定到这些）
        //---------------------------------------------------------------------
        [BindProperty(SupportsGet = true)] public long? OrgId { get; set; }
        [BindProperty(SupportsGet = true)] public long? UserId { get; set; }
        [BindProperty(SupportsGet = true)] public string UserName { get; set; }


        // 简单绑定用列表（SSR 输出）
        public List<WorkDeskStatCard> ObjectStatCards { get; set; } = new List<WorkDeskStatCard>();
        public List<WorkDeskStatCard> HazardStatCards { get; set; } = new List<WorkDeskStatCard>();


        /// <summary>根据 UserId 反查的真实姓名（SSR 回显 / 初始值）。</summary>
        public string UserRealName { get; private set; }

        // 管理员（有用户查看权限）可切换到其它中心/人员视图，非管理员只能看自己
        public bool CanChangeScope => Auth.CheckPower(Power.AdminLeader);

        // 实际责任网格命中范围：URL(管理员) > 用户 EffectiveAuthOrgId 展开后的全部子孙
        public List<long> CurrOrgIds => ResolveCurrOrgIds();

        // 责任网格根节点（未展开）：用于 URL 拼接，避免 Query 超长
        public List<long> CurrRootOrgIds => ResolveCurrRootOrgIds();

        // 实际要查询的目标用户：URL(管理员) > 当前登录用户
        public long CurrUserId
        {
            get
            {
                if (CanChangeScope && UserId.HasValue && UserId.Value > 0)
                    return UserId.Value;
                var uid = GetUserId();
                return uid ?? 0L;
            }
        }


        //---------------------------------------------------------------------
        // 页面加载：统计计数 + 卡片初始化
        //---------------------------------------------------------------------
        public void OnGet()
        {
            // UserId：URL(管理员) > 当前登录用户（默认显示当前用户
            UserId = Asp.GetQuery<long>("userId");
            if (!UserId.HasValue || UserId.Value <= 0)
                UserId = GetUserId();

            ResolveUserRealName();
            var today = DateTime.Today;
            var near  = today.AddDays(7);

            var objects = BuildObjectScopeQuery();
            var CountMyObjects = objects.Count();

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
            var CountUncheckedObjects = projection
                .Count(o => (o.IsChecked == null || o.IsChecked == false) || o.LastCheckDt == null);
            var CountNearExpireObjects = projection
                .Count(o =>
                {
                    var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                    return next.HasValue && next.Value > today && next.Value <= near;
                });
            var CountOverdueObjects = projection
                .Count(o =>
                {
                    var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                    return next.HasValue && next.Value <= today;
                });

            //
            ObjectStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("我的对象",         CountMyObjects,         AppendObjectScope($"/Checks/CheckObjects?isDel=false"), "fas fa-warehouse"),
                new WorkDeskStatCard("未巡查对象",       CountUncheckedObjects,  AppendObjectScope($"/Checks/CheckObjects?isDel=false&isChecked=false"), "fas fa-circle-check"),
                new WorkDeskStatCard("临期巡查对象",     CountNearExpireObjects, AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck={today.AddDays(1):yyyy-MM-dd},{today.AddDays(7):yyyy-MM-dd}"), "fas fa-clock"),
                new WorkDeskStatCard("超期未巡查对象",   CountOverdueObjects,    AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck=,{today:yyyy-MM-dd}"), "fas fa-triangle-exclamation"),
                new WorkDeskStatCard("重点监管企业",     CountObjectsByTag("重点监管"), AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagName={Uri.EscapeDataString("重点监管")}"), "fas fa-exclamation-triangle"),
                new WorkDeskStatCard("示范企业",        CountObjectsByTag("示范企业"),  AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagName={Uri.EscapeDataString("示范企业")}"), "fas fa-star"),
                new WorkDeskStatCard("八类高危企业",      CountObjectsByTag("八类高危企业"),    AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagName={Uri.EscapeDataString("八类高危企业")}"), "fas fa-industry"),
                new WorkDeskStatCard("三场所三企业",      CountObjectsByTag("三场所三企业"),    AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagName={Uri.EscapeDataString("三场所三企业")}"), "fas fa-store"),
            };

            var hazards = BuildHazardScopeQuery();
            var CountMyHazards       = hazards.Count();
            var CountPendingHazards  = hazards.Where(h => h.Status == CheckHazardStatus.Waiting || h.Status == CheckHazardStatus.Processing).Count();
            var CountOverdueHazards  = hazards.Where(h => h.Status != CheckHazardStatus.Archived && h.ExpireDt.HasValue && h.ExpireDt.Value <= today).Count();
            HazardStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("我发现的隐患",   CountMyHazards,      AppendHazardScope($"/Checks/Flow/CheckHazards"), "fas fa-eye"),
                new WorkDeskStatCard("待处理隐患",     CountPendingHazards, AppendHazardScope($"/Checks/Flow/CheckHazards?status=0,1"), "fas fa-inbox"),
                new WorkDeskStatCard("超期隐患",       CountOverdueHazards, AppendHazardScope($"/Checks/Flow/CheckHazards?excludeArchived=true&expireTo={today:yyyy-MM-dd}"), "fas fa-skull-crossbones"),
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
        // 我的任务：只显示当前选择人员相关的任务
        //   taskTab=unfinished(默认)：未完成（进度 < 100%）
        //   taskTab=all：全部
        // 相关判定：创建人 OR 任务承接组织命中 OR 检查对象的网格员/组织命中
        //---------------------------------------------------------------------
        public IActionResult OnGetMyTasks(Paging pi, string taskTab)
        {
            var uid = CurrUserId;
            var orgIds = CurrOrgIds;

            IQueryable<CheckTask> q = App.DAL.CheckTask.Search(null, null, null)
                .AsNoTracking()
                .Include(t => t.Creator).ThenInclude(u => u.Org)
                .Include(t => t.Org)
                .Include(t => t.CheckObjects);

            // 只看与当前人员相关的任务：创建人 / 任务承接组织 / 检查对象网格员 / 检查对象组织
            q = q.Where(t =>
                (uid > 0 && t.CreatorId == uid)
                || (orgIds.Count > 0 && t.OrgId.HasValue && orgIds.Contains(t.OrgId.Value))
                || t.CheckObjects.Any(co =>
                    (uid > 0 && (co.CheckerId == uid || co.Object.CheckerId == uid))
                    || (orgIds.Count > 0 && co.OrgId.HasValue && orgIds.Contains(co.OrgId.Value))
                    || (orgIds.Count > 0 && co.Object.DutyOrgId.HasValue && orgIds.Contains(co.Object.DutyOrgId.Value))
                ));

            // tab 过滤
            taskTab = taskTab ?? "unfinished";
            if (taskTab == "unfinished")
                q = q.Where(t => t.Progress == null || t.Progress < 100 || t.FinishCount < t.TotalCount);

            return BuildResult(0, "success", MaterializeAndProject(q, pi, uid > 0 ? uid : (long?)null), pi);
        }


        //---------------------------------------------------------------------
        // 内部：EF 查询先 ToList（避免 EF 翻译本地函数/字符串拼接失败），再手动 SortPage 成 WorkDeskTaskRow
        //---------------------------------------------------------------------
        /// <summary>将 EF 查询投影为 WorkDeskTaskRow</summary>
        private static List<object> MaterializeAndProject(IQueryable<CheckTask> query, Paging pi, long? checkerId)
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
                    LevelText    = t.Org != null
                                 ? (t.Org.FullName ?? t.Org.Name ?? "")
                                 : "",
                    CreatorText  = t.CreatorName,
                    CreatorOrg   = t.Creator?.Org?.Name ?? "",
                    Progress     = (int)(t.Progress ?? 0),
                    ProgressText = ComputeProgressText(t),
                    StartDt      = t.StartDt,
                    ExpireDt     = t.ExpireDt,
                    DetailUrl    = $"/Checks/Flow/CheckTaskForm?id={t.Id}",
                    CheckerId    = checkerId,
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
                UserRealName = u?.RealName;
            }
        }

        private List<long> ResolveCurrOrgIds()
        {
            var ids = ResolveCurrRootOrgIds();
            if (ids.Count == 0) return new List<long>();
            return OrgDescendants(ids);
        }

        private List<long> ResolveCurrRootOrgIds()
        {
            if (CanChangeScope && OrgId.HasValue && OrgId.Value > 0)
                return new List<long> { OrgId.Value };

            var user = App.DAL.User.Get(CurrUserId);
            if (user == null) return new List<long>();

            // 简化：责任组织只取授权组织 EffectiveAuthOrgId（AuthOrgId ?? OrgId），不再把 OrgId 和 AuthOrgIds 拼接
            var id = user.EffectiveAuthOrgId;
            if (id.HasValue && id.Value > 0) return new List<long> { id.Value };
            return new List<long>();
        }


        //---------------------------------------------------------------------
        // 内部：对象 / 隐患 范围
        //---------------------------------------------------------------------
        private IQueryable<CheckObject> BuildObjectScopeQuery()
        {
            var uid    = CurrUserId;
            var orgIds = CurrOrgIds;
            return App.DAL.CheckObject.Search(isDel: false)
                .Where(o => (orgIds.Count > 0 && o.DutyOrgId.HasValue && orgIds.Contains(o.DutyOrgId.Value))
                         || (o.CheckerId == uid));
        }

        /// <summary>按标签名称统计当前 scope 下的对象数量（标签含子孙标签）。</summary>
        private int CountObjectsByTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return 0;
            var tag = App.DAL.CheckTag.Set.FirstOrDefault(t => t.Name == tagName);
            if (tag == null) return 0;
            var tagIds = App.DAL.CheckTag.GetChildIds(tag.Id);
            if (tagIds.Count == 0) return 0;
            return BuildObjectScopeQuery()
                .Where(o => App.DAL.CheckObjectTag.IncludeSet.Any(ct => ct.CheckObjectId == o.Id && tagIds.Contains(ct.TagId)))
                .Count();
        }

        private IQueryable<CheckHazard> BuildHazardScopeQuery()
        {
            var uid    = CurrUserId;
            var orgIds = CurrOrgIds;
            return App.DAL.CheckHazard.Search(null, null, null, null, null, null)
                .Where(h => h.CheckerId == uid
                         || (orgIds.Count > 0
                             && h.Object != null
                             && h.Object.DutyOrgId.HasValue
                             && orgIds.Contains(h.Object.DutyOrgId.Value)));
        }

        //---------------------------------------------------------------------
        // 内部：进度文本计算
        //---------------------------------------------------------------------
        private static string ComputeProgressText(CheckTask t)
        {
            if (t.ExpireDt.HasValue && t.ExpireDt.Value <= DateTime.Now) return "超期";
            if (t.TotalCount > 0 && t.FinishCount >= t.TotalCount)      return "已完成";
            return "进行中";
        }

        //---------------------------------------------------------------------
        // 卡片 URL 拼接：把当前 scope（checkerId/dutyOrgId）追加到目标列表页 URL 查询参数上
        //---------------------------------------------------------------------
        /// <summary>卡片 URL 拼接：把当前 scope（checkerId + dutyOrgId）追加到目标列表页 URL 查询参数上</summary>
        private string AppendObjectScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl ?? string.Empty;
            var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);

            var uid = CurrUserId;
            if (uid > 0) qs["checkerId"] = uid.ToString();

            var roots = CurrRootOrgIds;
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

            var uid = CurrUserId;
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


}
