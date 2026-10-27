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
        public List<long> CurrOrgIds => ResolveCurrOrgIds();   //   实际责任网格命中范围：URL(管理员) > 用户 EffectiveAuthOrgId 展开后的全部子孙
        public List<long> CurrRootOrgIds => ResolveCurrRootOrgIds();  // 责任网格根节点（未展开）：用于 URL 拼接，避免 Query 超长

        
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

            var objects = BuildObjectScopeQuery();
            var CountMyObjects = objects.Count();

            // ？？？
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
            var CountUncheckedObjects = projection.Count(o => (o.IsChecked == null || o.IsChecked == false) || o.LastCheckDt == null);
            var CountNearExpireObjects = projection
                .Count(o =>
                {
                    var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                    // 与 CheckObjects 的 nextCheck 解析对齐：按日期比较，临期范围 [today+1, today+7]
                    return next.HasValue && next.Value.Date >= today.AddDays(1) && next.Value.Date <= today.AddDays(7);
                });
            var CountOverdueObjects = projection
                .Count(o =>
                {
                    var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                    // 与 CheckObjects 的 nextCheck 解析对齐：按日期比较，超期范围 (-inf, today]
                    return next.HasValue && next.Value.Date <= today;
                });

            // 初始化对象统计卡片
            ObjectStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("我的对象",         CountMyObjects,         AppendObjectScope($"/Checks/CheckObjects?isDel=false"), "fas fa-warehouse"),
                new WorkDeskStatCard("未巡查对象",       CountUncheckedObjects,  AppendObjectScope($"/Checks/CheckObjects?isDel=false&isChecked=false"), "fas fa-circle-check"),
                new WorkDeskStatCard("临期巡查对象",     CountNearExpireObjects, AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck={today.AddDays(1):yyyy-MM-dd},{today.AddDays(7):yyyy-MM-dd}"), "fas fa-clock"),
                new WorkDeskStatCard("超期未巡查对象",   CountOverdueObjects,    AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck=,{today:yyyy-MM-dd}"), "fas fa-triangle-exclamation"),
            };

            SuperviseStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("重点监管企业",     CountObjectsByTag("重点监管"), AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagName={Uri.EscapeDataString("重点监管")}"), "fas fa-exclamation-triangle"),
                new WorkDeskStatCard("示范企业",        CountObjectsByTag("示范企业"),  AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagName={Uri.EscapeDataString("示范企业")}"), "fas fa-star"),
            };

            RiskStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("八类高危企业",      CountObjectsByTag("八类高危企业"),    AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagName={Uri.EscapeDataString("八类高危企业")}"), "fas fa-industry"),
                new WorkDeskStatCard("三场所三企业",      CountObjectsByTag("三场所三企业"),    AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagName={Uri.EscapeDataString("三场所三企业")}"), "fas fa-store"),
            };

            // 初始化隐患统计卡片
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
                q = q.Where(t => !t.IsFinished);

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
                IsFinished = t.IsFinished,
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
            var id = user.AuthOrgId;
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
                .Where(o => (orgIds.Count > 0 && o.OrgId.HasValue && orgIds.Contains(o.OrgId.Value))
                         || (o.CheckerId == uid));
        }

        /// <summary>按标签名称统计当前 scope 下的对象数量（标签含子孙标签）。</summary>
        private int CountObjectsByTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName)) return 0;
            var tag = App.DAL.CheckTag.Set.FirstOrDefault(t => t.Name == tagName && t.TagType == CheckTagType.Basic);
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
                             && h.CheckObject != null
                             && h.CheckObject.OrgId.HasValue
                             && orgIds.Contains(h.CheckObject.OrgId.Value)));
        }

        //---------------------------------------------------------------------
        // 卡片 URL 拼接：把当前 scope（checkerId/orgId）追加到目标列表页 URL 查询参数上
        //---------------------------------------------------------------------
        /// <summary>卡片 URL 拼接：把当前 scope（checkerId + orgId）追加到目标列表页 URL 查询参数上</summary>
        private string AppendObjectScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl ?? string.Empty;
            var qs = System.Web.HttpUtility.ParseQueryString(string.Empty);

            var uid = CurrUserId;
            if (uid > 0) qs["checkerId"] = uid.ToString();

            var roots = CurrRootOrgIds;
            if (roots != null && roots.Count == 1)
                qs["orgId"] = roots[0].ToString();
            else if (roots != null && roots.Count > 1)
                qs["orgIds"] = string.Join(",", roots);

            // 工作台对象统计按“本人负责对象 OR 本人授权组织内对象”计算，列表需保持同一语义。
            if (uid > 0 && roots != null && roots.Count > 0)
                qs["scopeCheckerOrOrg"] = "true";

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
