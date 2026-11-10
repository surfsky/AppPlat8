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
        public string UserRealName { get; private set; }  // 用户真实姓名
        public bool CanChangeScope => Auth.HasPower(Power.AdminLeader);  // 是否可切换用户名
        private List<long> _currOrgIds;
        private List<long> _currRootOrgIds;
        public List<long> CurrOrgIds => _currOrgIds ??= ResolveCurrOrgIds();   // 实际责任网格范围（含子孙）
        public List<long> CurrRootOrgIds => _currRootOrgIds ??= ResolveCurrRootOrgIds();  // 责任网格根节点，用于 URL 拼接

        
        public List<WorkDeskStatCard> ObjectStatCards { get; set; } = new List<WorkDeskStatCard>();
        public List<WorkDeskStatCard> SuperviseStatCards { get; set; } = new List<WorkDeskStatCard>();
        public List<WorkDeskStatCard> RiskStatCards { get; set; } = new List<WorkDeskStatCard>();
        public List<WorkDeskStatCard> HazardStatCards { get; set; } = new List<WorkDeskStatCard>();

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
        // 页面加载只初始化卡片和链接，统计通过独立请求加载。
        //---------------------------------------------------------------------
        public void OnGet()
        {
            // UserId：URL(管理员) > 当前登录用户（默认显示当前用户
            UserId = Asp.GetQuery<long>("userId");
            if (!UserId.HasValue || UserId.Value <= 0)
                UserId = GetUserId();

            ResolveUserRealName();
            ObjectStatCards = BuildObjectCards(false);
            SuperviseStatCards = BuildSuperviseCards(false);
            RiskStatCards = BuildRiskCards(false);
            HazardStatCards = BuildHazardCards(false);
        }

        /// <summary>按分组加载统计，沿用工作台原有用户及组织授权范围。</summary>
        public IActionResult OnGetStats(string group)
        {
            var cards = group switch
            {
                "objects" => BuildObjectCards(true),
                "supervise" => BuildSuperviseCards(true),
                "risk" => BuildRiskCards(true),
                "hazards" => BuildHazardCards(true),
                _ => null
            };
            return cards == null ? BuildResult(400, "无效的统计分组") : BuildResult(0, "success", cards);
        }

        private List<WorkDeskStatCard> BuildObjectCards(bool includeCounts)
        {
            var today = DateTime.Today;
            var myObjectsCnt = 0;
            var uncheckedObjectsCnt = 0;
            var nearExpireObjectsCnt = 0;
            var overdueObjectsCnt = 0;
            if (includeCounts)
            {
                var projection = BuildObjectScopeQuery()
                    .Select(o => new { o.IsChecked, o.NextCheckDt })
                    .AsNoTracking()
                    .ToList();
                myObjectsCnt = projection.Count;
                uncheckedObjectsCnt = projection.Count(o => o.IsChecked == false);
                nearExpireObjectsCnt = projection.Count(o =>
                {
                    var next = o.NextCheckDt;
                    return next.HasValue && next.Value.Date >= today.AddDays(1) && next.Value.Date <= today.AddDays(7);
                });
                overdueObjectsCnt = projection.Count(o =>
                {
                    var next = o.NextCheckDt;
                    return next.HasValue && next.Value.Date <= today;
                });
            }

            return new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("我的对象",         myObjectsCnt,         AppendObjectScope($"/Checks/CheckObjects?isDel=false"), "fas fa-warehouse"),
                new WorkDeskStatCard("未巡查对象",       uncheckedObjectsCnt,  AppendObjectScope($"/Checks/CheckObjects?isDel=false&isChecked=false"), "fas fa-circle-check"),
                new WorkDeskStatCard("临期巡查对象",     nearExpireObjectsCnt, AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck={today.AddDays(1):yyyy-MM-dd},{today.AddDays(7):yyyy-MM-dd}"), "fas fa-clock"),
                new WorkDeskStatCard("超期未巡查对象",   overdueObjectsCnt,    AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck=,{today:yyyy-MM-dd}"), "fas fa-triangle-exclamation"),
            };

        }

        private List<WorkDeskStatCard> BuildSuperviseCards(bool includeCounts)
        {
            return new List<WorkDeskStatCard>
            {
                CreateTagSpecCard("重点监管企业", "重点监管", "fas fa-exclamation-triangle", includeCounts),
                CreateTagSpecCard("示范企业", "示范企业", "fas fa-star", includeCounts),
            };
        }

        private List<WorkDeskStatCard> BuildRiskCards(bool includeCounts)
        {
            return new List<WorkDeskStatCard>
            {
                CreateTagSpecCard("八类高危企业", "八类高危企业", "fas fa-industry", includeCounts),
                CreateTagSpecCard("三场所三企业", "三场所三企业", "fas fa-store", includeCounts),
            };
        }

        private List<WorkDeskStatCard> BuildHazardCards(bool includeCounts)
        {
            var today = DateTime.Today;
            var CountMyHazards = 0;
            var CountPendingHazards = 0;
            var CountOverdueHazards = 0;
            if (includeCounts)
            {
                var hazards = BuildHazardScopeQuery();
                CountMyHazards = hazards.Count();
                CountPendingHazards = hazards.Where(h => h.Status == CheckHazardStatus.Waiting || h.Status == CheckHazardStatus.Processing).Count();
                CountOverdueHazards = hazards.Where(h => h.Status != CheckHazardStatus.Archived && h.ExpireDt.HasValue && h.ExpireDt.Value <= today).Count();
            }
            return new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("我发现的隐患",   CountMyHazards,      AppendHazardScope($"/Checks/Flow/CheckHazards"), "fas fa-eye"),
                new WorkDeskStatCard("待处理隐患",     CountPendingHazards, AppendHazardScope($"/Checks/Flow/CheckHazards?status=0,1"), "fas fa-inbox"),
                new WorkDeskStatCard("超期隐患",       CountOverdueHazards, AppendHazardScope($"/Checks/Flow/CheckHazards?excludeArchived=true&expireTo={today:yyyy-MM-dd}"), "fas fa-skull-crossbones"),
            };
        }

        //---------------------------------------------------------------------
        // 我的任务对象：显示当前用户相关的 CheckTaskObject 记录
        //   taskObjTab=unfinished(默认)：未完成
        //   taskObjTab=all：全部
        //---------------------------------------------------------------------
        public IActionResult OnGetMyTaskObjects(Paging pi, string taskObjTab)
        {
            var uid = CurrUserId;
            var orgIds = CurrOrgIds;

            IQueryable<CheckTaskObject> q = CheckTaskObject.Set
                .AsNoTracking()
                .Include(t => t.Task)
                .Include(t => t.CheckObject).ThenInclude(o => o.Org)
                .Include(t => t.CheckObject).ThenInclude(o => o.Checker)
                .Include(t => t.Checker);

            // 只看与当前人员相关的任务对象：本人是检查人 / 任务对象的责任组织命中 / 对象的网格员
            q = q.Where(t =>
                (uid > 0 && t.CheckerId == uid)
                || (uid > 0 && t.CheckObject.CheckerId == uid)
                || (orgIds.Count > 0 && t.OrgId.HasValue && orgIds.Contains(t.OrgId.Value))
                || (orgIds.Count > 0 && t.CheckObject.OrgId.HasValue && orgIds.Contains(t.CheckObject.OrgId.Value)));

            // tab 过滤
            taskObjTab = taskObjTab ?? "unfinished";
            if (taskObjTab == "unfinished")
                q = q.Where(t => t.Status == CheckTaskObjectStatus.Unchecked || t.Status == null);

            // 默认按任务 Id + 排序号排序
            if (pi == null) pi = new Paging();
            if (pi.SortField.IsEmpty()) { pi.SortField = "TaskId"; pi.SortDirection = "ASC"; }

            var total = q.Count();
            pi.SetTotal(total);
            var list = q.SortAndPage(pi).ToList();

            var rows = list.Select(t => (object)new WorkDeskTaskObjectRow
            {
                Id = t.Id,
                TaskId = t.TaskId,
                TaskName = t.Task?.Name ?? "",
                CheckObjectId = t.CheckObjectId,
                ObjectName = t.CheckObject?.Name ?? "",
                DutyOrgName = t.CheckObject?.OrgName ?? t.CheckObject?.Org?.Name ?? "",
                CheckerName = t.Checker?.Name ?? t.CheckObject?.CheckerName ?? "",
                Status = t.Status,
                HazardCnt = t.HazardCnt,
                FixHazardCnt = t.FixHazardCnt,
                TaskStartDt = t.Task?.StartDt,
                TaskExpireDt = t.Task?.ExpireDt,
                DetailUrl = t.TaskId.HasValue ? $"/Checks/Flow/CheckTaskForm?id={t.TaskId.Value}" : "",
            }).ToList();

            return BuildResult(0, "success", rows, pi);
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
            var ids = CurrRootOrgIds;
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
            var id = user.AuthOrgId;
            if (id.HasValue && id.Value > 0) return new List<long> { id.Value };
            return new List<long>();
        }


        //---------------------------------------------------------------------
        // 内部：对象 / 隐患 范围
        //---------------------------------------------------------------------
        private IQueryable<CheckObject> BuildObjectScopeQuery()
        {
            var uid = CurrUserId;
            return App.DAL.CheckObject.Search(isDel: false)
                .Where(o => o.CheckerId == uid)
                .FilterByOrg(Auth.GetUser(), nameof(CheckObject.OrgId));
        }

        /// <summary>按扩展标签统计对象，并生成对应的列表链接。</summary>
        private WorkDeskStatCard CreateTagSpecCard(string title, string tagName, string iconCls, bool includeCounts)
        {
            var tags = App.DAL.CheckTag.Set.Where(t => t.TagType == CheckTagType.Spec);
            var tag = tags.FirstOrDefault(t => t.Name == tagName)
                ?? tags.Where(t => t.Name.Contains(tagName)).OrderBy(t => t.Name.Length).FirstOrDefault();
            var tagIds = !includeCounts || tag == null ? new List<long>() : App.DAL.CheckTag.GetChildIds(tag.Id);
            var count = tagIds.Count == 0
                ? 0
                : BuildObjectScopeQuery()
                    .Where(o => App.DAL.CheckObjectTagSpec.IncludeSet.Any(ct => ct.CheckObjectId == o.Id && tagIds.Contains(ct.TagId)))
                    .Count();
            var rawUrl = tag == null
                ? "/Checks/CheckObjects?isDel=false"
                : $"/Checks/CheckObjects?isDel=false&tagSpecIds={tag.Id}";
            return new WorkDeskStatCard(title, count, AppendObjectScope(rawUrl), iconCls);
        }

        private IQueryable<CheckHazard> BuildHazardScopeQuery()
        {
            var uid    = CurrUserId;
            var orgIds = CurrOrgIds;
            var q = App.DAL.CheckHazard.Search(null, null, null, null, null, null)
                .Where(h => h.CheckerId == uid
                         || (orgIds.Count > 0
                             && h.CheckObject != null
                             && h.CheckObject.OrgId.HasValue
                             && orgIds.Contains(h.CheckObject.OrgId.Value)));
            var user = Auth.GetUser();
            if (user != null && user.Name != "admin" && user.AuthOrgId.HasValue)
            {
                var allowed = OrgFilter.GetChildOrgIds(user.AuthOrgId);
                q = q.Where(h => h.CheckObject != null && h.CheckObject.OrgId.HasValue && allowed.Contains(h.CheckObject.OrgId.Value));
            }
            return q;
        }

        //---------------------------------------------------------------------
        // 卡片 URL 拼接：把当前 scope（checkerId/orgId）追加到目标列表页 URL 查询参数上
        //---------------------------------------------------------------------
        /// <summary>卡片 URL 拼接：把当前检查人追加到目标列表页 URL 查询参数上。</summary>
        private string AppendObjectScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl ?? string.Empty;
            var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);

            var uid = CurrUserId;
            if (uid > 0) qs["checkerId"] = uid.ToString();

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
            var roots = CurrRootOrgIds;
            if (roots.Count == 1) qs["orgId"] = roots[0].ToString();
            if (uid > 0) qs["scopeCheckerOrOrg"] = "true";

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
