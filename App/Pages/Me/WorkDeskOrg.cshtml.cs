using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Me
{
    /// <summary>科室工作台页面模型。</summary>
    public class WorkDeskOrgModel : AuthModel
    {
        [BindProperty(SupportsGet = true)] public long? OrgId { get; set; }  // 来自 URL 的组织 Id。
        [BindProperty(SupportsGet = true)] public string OrgName { get; set; }  // 来自 URL 的组织名称。
        public List<long> CurrRootOrgIds => ResolveCurrRootOrgIds();   // 当前生效的组织根节点列表。
        public List<long> CurrOrgIds => ResolveCurrOrgIds();   // 当前生效的组织范围（含子孙）。
        public string OrgDisplay { get; private set; }  // 当前组织显示名。
        public bool CanChangeScope => Auth.HasPower(Power.AdminLeader);  // 是否允许切换组织范围。
        public List<WorkDeskStatCard> ObjectStatCards { get; set; } = new List<WorkDeskStatCard>();     // 页面统计卡片：对象。
        public List<WorkDeskStatCard> SuperviseStatCards { get; set; } = new List<WorkDeskStatCard>();     // 页面统计卡片：监管对象。
        public List<WorkDeskStatCard> RiskStatCards { get; set; } = new List<WorkDeskStatCard>();     // 页面统计卡片：高风险对象。
        public List<WorkDeskStatCard> HazardStatCards { get; set; } = new List<WorkDeskStatCard>();     // 页面统计卡片：隐患。

        /// <summary>当前生效的组织根节点。</summary>
        public long EffectiveOrgId
        {
            get
            {
                if (CanChangeScope && OrgId.HasValue && OrgId.Value > 0)
                    return OrgId.Value;
                return this.GetUser()?.AuthOrgId ?? 0L;
            }
        }


        /// <summary>页面初始加载：计算统计卡片。</summary>
        public void OnGet()
        {
            ResolveOrgDisplay();

            var today = DateTime.Today;
            var objects = BuildObjectScopeQuery();
            var countObjects = objects.Count();

            // NextCheckDt 是计算属性，先投影后在内存里计算临期/超期。
            var projection = objects
                .Select(o => new
                {
                    o.IsChecked,
                    o.LastCheckDt,
                    o.RiskLevel
                })
                .AsNoTracking()
                .ToList();

            var countUncheckedObjects = projection.Count(o => (o.IsChecked == null || o.IsChecked == false) || o.LastCheckDt == null);
            var countNearExpireObjects = projection.Count(o =>
            {
                var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                return next.HasValue && next.Value.Date >= today.AddDays(1) && next.Value.Date <= today.AddDays(7);
            });
            var countOverdueObjects = projection.Count(o =>
            {
                var next = ComputeNextCheckDt(o.LastCheckDt, o.RiskLevel);
                return next.HasValue && next.Value.Date <= today;
            });

            ObjectStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("科室对象",         countObjects,           AppendObjectScope("/Checks/CheckObjects?isDel=false"), "fas fa-warehouse"),
                new WorkDeskStatCard("未巡查对象",       countUncheckedObjects,  AppendObjectScope("/Checks/CheckObjects?isDel=false&isChecked=false"), "fas fa-circle-check"),
                new WorkDeskStatCard("临期巡查对象",     countNearExpireObjects, AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck={today.AddDays(1):yyyy-MM-dd},{today.AddDays(7):yyyy-MM-dd}"), "fas fa-clock"),
                new WorkDeskStatCard("超期未巡查对象",   countOverdueObjects,    AppendObjectScope($"/Checks/CheckObjects?isDel=false&nextCheck=,{today:yyyy-MM-dd}"), "fas fa-triangle-exclamation"),
            };

            SuperviseStatCards = new List<WorkDeskStatCard>
            {
                CreateTagSpecCard("重点监管企业", "重点监管", "fas fa-exclamation-triangle"),
                CreateTagSpecCard("示范企业", "示范企业", "fas fa-star"),
            };

            RiskStatCards = new List<WorkDeskStatCard>
            {
                CreateTagSpecCard("八类高危企业", "八类高危企业", "fas fa-industry"),
                CreateTagSpecCard("三场所三企业", "三场所三企业", "fas fa-store"),
            };

            var hazards = BuildHazardScopeQuery();
            var countHazards = hazards.Count();
            var countPendingHazards = hazards.Where(h => h.Status == CheckHazardStatus.Waiting || h.Status == CheckHazardStatus.Processing).Count();
            var countOverdueHazards = hazards.Where(h => h.Status != CheckHazardStatus.Archived && h.ExpireDt.HasValue && h.ExpireDt.Value <= today).Count();

            HazardStatCards = new List<WorkDeskStatCard>
            {
                new WorkDeskStatCard("科室隐患",   countHazards,        AppendHazardScope("/Checks/Flow/CheckHazards"), "fas fa-eye"),
                new WorkDeskStatCard("待处理隐患", countPendingHazards, AppendHazardScope("/Checks/Flow/CheckHazards?status=0,1"), "fas fa-inbox"),
                new WorkDeskStatCard("超期隐患",   countOverdueHazards, AppendHazardScope($"/Checks/Flow/CheckHazards?excludeArchived=true&expireTo={today:yyyy-MM-dd}"), "fas fa-skull-crossbones"),
            };
        }

        /// <summary>获取科室专项检查任务对象列表。</summary>
        public IActionResult OnGetOrgTaskObjects(Paging pi, string taskObjTab)
        {
            var orgIds = CurrOrgIds;

            IQueryable<CheckTaskObject> q = CheckTaskObject.Set
                .AsNoTracking()
                .Include(t => t.Task)
                .Include(t => t.CheckObject).ThenInclude(o => o.Org)
                .Include(t => t.CheckObject).ThenInclude(o => o.Checker)
                .Include(t => t.Checker);

            q = q.Where(t =>
                (orgIds.Count > 0 && t.OrgId.HasValue && orgIds.Contains(t.OrgId.Value))
                || (orgIds.Count > 0 && t.CheckObject.OrgId.HasValue && orgIds.Contains(t.CheckObject.OrgId.Value)));

            taskObjTab = taskObjTab ?? "unfinished";
            if (taskObjTab == "unfinished")
                q = q.Where(t => !t.IsFinished);

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

        /// <summary>按扩展标签统计对象，并生成对应的列表链接。</summary>
        private WorkDeskStatCard CreateTagSpecCard(string title, string tagName, string iconCls)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return new WorkDeskStatCard(title, 0, AppendObjectScope("/Checks/CheckObjects?isDel=false"), iconCls);

            var tags = App.DAL.CheckTag.Set.Where(t => t.TagType == CheckTagType.Spec);
            var tag = tags.FirstOrDefault(t => t.Name == tagName)
                ?? tags.Where(t => t.Name.Contains(tagName)).OrderBy(t => t.Name.Length).FirstOrDefault();
            if (tag == null)
                return new WorkDeskStatCard(title, 0, AppendObjectScope("/Checks/CheckObjects?isDel=false"), iconCls);

            var tagIds = App.DAL.CheckTag.GetChildIds(tag.Id);
            var count = tagIds.Count == 0
                ? 0
                : BuildObjectScopeQuery()
                    .Where(o => App.DAL.CheckObjectTagSpec.IncludeSet.Any(ct => ct.CheckObjectId == o.Id && tagIds.Contains(ct.TagId)))
                    .Count();
            var url = AppendObjectScope($"/Checks/CheckObjects?isDel=false&tagSpecIds={tag.Id}");
            return new WorkDeskStatCard(title, count, url, iconCls);
        }

        /// <summary>解析当前组织显示名。</summary>
        private void ResolveOrgDisplay()
        {
            if (OrgId.HasValue && OrgId.Value > 0)
            {
                var org = App.DAL.Org.Get(OrgId.Value);
                if (org != null)
                {
                    OrgDisplay = org.Name;
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(OrgName))
            {
                var keyword = OrgName.Trim();
                var org = App.DAL.Org.All.FirstOrDefault(x => x.Name == keyword || x.FullName == keyword);
                if (org != null)
                {
                    OrgId = org.Id;
                    OrgDisplay = org.Name;
                    return;
                }
            }

            if (EffectiveOrgId > 0)
            {
                var org = App.DAL.Org.Get(EffectiveOrgId);
                if (org != null)
                {
                    if (!OrgId.HasValue || OrgId.Value <= 0)
                        OrgId = org.Id;
                    OrgDisplay = org.Name;
                    return;
                }
            }

            OrgDisplay = "请选择组织";
        }

        /// <summary>构建对象范围查询。</summary>
        private IQueryable<CheckObject> BuildObjectScopeQuery()
        {
            var orgIds = CurrOrgIds;
            return App.DAL.CheckObject.Search(isDel: false)
                .Where(o => orgIds.Count > 0 && o.OrgId.HasValue && orgIds.Contains(o.OrgId.Value));
        }

        /// <summary>构建隐患范围查询。</summary>
        private IQueryable<CheckHazard> BuildHazardScopeQuery()
        {
            var orgIds = CurrOrgIds;
            return App.DAL.CheckHazard.Search(null, null, null, null, null, null)
                .Where(h => orgIds.Count > 0
                         && h.CheckObject != null
                         && h.CheckObject.OrgId.HasValue
                         && orgIds.Contains(h.CheckObject.OrgId.Value));
        }

        /// <summary>把当前组织范围追加到对象列表 URL。</summary>
        private string AppendObjectScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
                return rawUrl ?? string.Empty;

            var qs = HttpUtility.ParseQueryString(string.Empty);
            var roots = CurrRootOrgIds;

            if (roots.Count == 1)
                qs["orgId"] = roots[0].ToString();
            else if (roots.Count > 1)
                qs["orgIds"] = string.Join(",", roots);

            if (roots.Count > 0)
                qs["scopeOrgOnly"] = "true";

            if (qs.Count == 0)
                return rawUrl;

            var sep = rawUrl.Contains('?') ? '&' : '?';
            return rawUrl + sep + qs.ToString();
        }

        /// <summary>把当前组织范围追加到隐患列表 URL。</summary>
        private string AppendHazardScope(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
                return rawUrl ?? string.Empty;

            var qs = HttpUtility.ParseQueryString(string.Empty);
            var roots = CurrRootOrgIds;

            if (roots.Count == 1)
                qs["orgId"] = roots[0].ToString();
            else if (roots.Count > 1)
                qs["orgIds"] = string.Join(",", roots);

            if (roots.Count > 0)
                qs["scopeOrgOnly"] = "true";

            if (qs.Count == 0)
                return rawUrl;

            var sep = rawUrl.Contains('?') ? '&' : '?';
            return rawUrl + sep + qs.ToString();
        }

        /// <summary>计算当前组织根节点列表。</summary>
        private List<long> ResolveCurrRootOrgIds()
        {
            if (EffectiveOrgId > 0)
                return new List<long> { EffectiveOrgId };
            return new List<long>();
        }

        /// <summary>计算当前组织范围列表。</summary>
        private List<long> ResolveCurrOrgIds()
        {
            var roots = ResolveCurrRootOrgIds();
            if (roots.Count == 0)
                return new List<long>();

            return App.DAL.Org.All
                .GetDescendants(roots)
                .Cast<App.DAL.Org>()
                .Select(o => o.Id)
                .Distinct()
                .ToList();
        }

        /// <summary>计算下一次巡查时间。</summary>
        private static DateTime? ComputeNextCheckDt(DateTime? latestCheckDt, CheckRiskLevel? riskLevel)
        {
            if (!latestCheckDt.HasValue)
                return null;
            return latestCheckDt.Value.AddMonths(GetCheckCycleMonths(riskLevel));
        }

        /// <summary>计算巡查周期月份数。</summary>
        private static int GetCheckCycleMonths(CheckRiskLevel? riskLevel)
        {
            return riskLevel switch
            {
                CheckRiskLevel.None => 12,
                CheckRiskLevel.Low => 9,
                CheckRiskLevel.Medium => 6,
                CheckRiskLevel.High => 3,
                _ => 12
            };
        }
    }
}
