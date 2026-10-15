using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using App.EleUI;
using App.Web;
using App.BLL;
using App.HttpApi;

namespace App.Pages.Checks
{
    [Auth(Power.CheckObjectView)]
    public class CheckObjectsModel : AuthModel
    {
        public CheckObject Item { get; set; } = new CheckObject();         // 检查对象实体（用于页面数据绑定）
        public List<long> DutyOrgIds { get; set; } = new List<long>();     // 多值，用于 ElePicker/Select 控件回显
        public long? DefaultCheckerId { get; set; }      // 默认检查人ID
        public string DefaultCheckerName { get; set; }  // 默认检查人名称
        public long? DutyOrgId { get; set; }  // 单值，用于 ElePicker/Select 控件单独匹配
        public string ScopeTagIds { get; set; }  // 多值，用于标签树控件回显

        
        //-------------------------------------------------------------------------
        // 输入参数控制
        //-------------------------------------------------------------------------
        public void OnGet()
        {
            // 兼容：dutyOrgId（单值，来自报表跳转）→ 优先写入 DutyOrgId 和 DutyOrgIds
            var rawDutyOrgId = Request.Query["dutyOrgId"].FirstOrDefault();
            if (long.TryParse(rawDutyOrgId, out var singleOrgId) && singleOrgId > 0)
            {
                DutyOrgId = singleOrgId;
                Item.DutyOrgId = singleOrgId;
                if (DutyOrgIds.Count == 0)
                    DutyOrgIds = new List<long> { singleOrgId };
                goto parseChecker;
            }

            var qs = Request.Query["dutyOrgIds"].ToString();
            if (!string.IsNullOrWhiteSpace(qs))
            {
                foreach (var s in qs.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (long.TryParse(s.Trim(), out var id)) 
                        DutyOrgIds.Add(id);
                }
                if (DutyOrgIds.Count > 0) goto parseChecker;
            }

            var user = GetUser();
            if (user != null)
            {
                var authOrgIds = user.AuthOrgIds;
                if (authOrgIds != null && authOrgIds.Count > 0)
                    DutyOrgIds = authOrgIds.Distinct().ToList();
                else if (user.OrgId > 0)
                    DutyOrgIds = new List<long> { user.OrgId.Value };
            }

        parseChecker:
            // URL 参数兼容: 标准名 checkerId + 别名 checkId（用户口头简称）
            var rawCheckerId = Request.Query["checkerId"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(rawCheckerId))
                rawCheckerId = Request.Query["checkId"].FirstOrDefault();
            if (long.TryParse(rawCheckerId, out var cid))
            {
                DefaultCheckerId = cid;
                Item.CheckerId = cid;
                var u = App.DAL.User.Get(cid);
                if (u != null)
                    DefaultCheckerName = u.RealName.IsNotEmpty() ? u.RealName : u.Name;
            }
            else
            {
                // 无 URL 参数时，默认显示当前用户
                var cu = GetUser();
                if (cu != null && cu.Id > 0)
                {
                    DefaultCheckerId = cu.Id;
                    Item.CheckerId = cu.Id;
                    DefaultCheckerName = cu.RealName.IsNotEmpty() ? cu.RealName : cu.Name;
                }
            }

            // 解析 tagName → 仅绑定父标签 ID 到 UI（子标签由 Search 内部展开查询）
            var rawTagName = Request.Query["tagName"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawTagName))
            {
                var tag = CheckTag.Set.FirstOrDefault(t => t.Name == rawTagName);
                if (tag != null)
                {
                    ScopeTagIds = tag.Id.ToString();
                    Item.TagIds = new List<long> { tag.Id };
                }
            }
        }

        /// <summary>解析 tagIds 参数：兼容表单提交（List<long>）+ EleTreePicker 的「?tagIds=127,136,138」逗号字符串，统一去重去空。</summary>
        static List<long> ParseTagIds(List<long> fromBinder, Microsoft.Extensions.Primitives.StringValues raw)
        {
            var list = new List<long>();
            if (fromBinder != null) list.AddRange(fromBinder);
            if (raw.Count > 0)
            {
                foreach (var s in raw)
                {
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    foreach (var seg in s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (long.TryParse(seg.Trim(), out var id) && id > 0) list.Add(id);
                    }
                }
            }
            return list.Distinct().ToList();
        }

        /// <summary>解析 dutyOrgIds：与 tagIds 同样式（?dutyOrgIds=1,2,3），用于 WorkDesk 多值 scope 透传。</summary>
        static List<long> ParseOrgIds(List<long> fromBinder, Microsoft.Extensions.Primitives.StringValues raw)
        {
            var list = new List<long>();
            if (fromBinder != null) list.AddRange(fromBinder);
            if (raw.Count > 0)
            {
                foreach (var s in raw)
                {
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    foreach (var seg in s.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (long.TryParse(seg.Trim(), out var id) && id > 0) list.Add(id);
                    }
                }
            }
            return list.Distinct().ToList();
        }        

        //-------------------------------------------------------------------------
        // 查询
        //-------------------------------------------------------------------------
        /// <summary>查询风险点清单</summary>
        public IActionResult OnGetData(
            Paging pi, 
            string name="", 
            string code="",
            string socialCreditCode="", 
            string address="",
            string dutyUserName="",
            bool? hasHarzard=null,
            bool? isChecked=null,
            long? dutyOrgId=null, 
            List<long> dutyOrgIds=null,
            long? checkerId=null,
            long? checkId=null, 
            CheckObjectType? objectType=null, 
            CheckScope? scope=null,
            CheckObjectScale? scale=null, 
            CheckRiskLevel? riskLevel=null,
            CheckIndustryType? industryType=null,
            List<DateTime> createDt=null,
            List<DateTime> lastCheckDt=null,
            List<long> tagIds=null,
            string tagName=null,
            bool? isDel=null
            )
        {
            tagIds = ParseTagIds(tagIds, Request.Query["tagIds"]);
            // 兼容 tagName：按标签名称解析为父标签 ID（子标签由 CheckObject.Search 的 GetTagIdGroups 展开）
            if (tagIds == null || tagIds.Count == 0)
            {
                var rawTagName = tagName.IsEmpty() ? Request.Query["tagName"].FirstOrDefault() : tagName;
                if (rawTagName.IsNotEmpty())
                {
                    var tag = CheckTag.Set.FirstOrDefault(t => t.Name == rawTagName);
                    if (tag != null) tagIds = new List<long> { tag.Id };
                }
            }
            dutyOrgIds = ParseOrgIds(dutyOrgIds, Request.Query["dutyOrgIds"]);
            DateTime? createStartDt = createDt.GetVal(0);
            DateTime? createEndDt = createDt.GetVal(1);
            DateTime? lastCheckStartDt = lastCheckDt.GetVal(0);
            DateTime? lastCheckEndDt = lastCheckDt.GetVal(1);
            var effCheckerId = checkId ?? checkerId;

            DateTime? nextCheckFrom = null;
            DateTime? nextCheckTo = null;
            var rawNext = Request.Query["nextCheck"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawNext))
            {
                // 不能用 RemoveEmptyEntries，否则 ",2026-10-14" 会丢失空的 from，导致 to 被当成 from
                var parts = rawNext.Split(',');
                // 约定：URL 传两个日期时，[from, to] 都是闭区间（含端点当日），按卡片范围 WorkDesk 定义：
                //   临期 nextCheck={today+1},{today+7}  =>  from=today+1.Date to=today+7.Date.EndOfDay
                //   超期 nextCheck=,{today}              =>  from=null      to=today.Date.EndOfDay（闭区间，NextCheck<=today）
                if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var d0)) nextCheckFrom = d0.Date;
                if (parts.Length >= 2 && DateTime.TryParse(parts[1], out var d1)) nextCheckTo = d1.Date.AddDays(1).AddTicks(-1);
            }

            // WorkDesk "我的对象" scope：checkerId 与 dutyOrg 同时存在时用 OR 语义
            // （与 WorkDesk.BuildObjectScopeQuery 对齐：DutyOrgId ∈ orgIds OR CheckerId == uid）
            var hasCheckerScope = effCheckerId.HasValue && effCheckerId.Value > 0;
            var hasOrgScope = (dutyOrgId.HasValue && dutyOrgId.Value > 0) || (dutyOrgIds != null && dutyOrgIds.Count > 0);
            var useOrScope = hasCheckerScope && hasOrgScope;
            long? searchCheckerId = useOrScope ? null : effCheckerId;
            long? searchDutyOrgId = useOrScope ? null : dutyOrgId;
            List<long> searchDutyOrgIds = useOrScope ? null : dutyOrgIds;

            var q = CheckObject.Search(
                name: name, 
                code: code,
                isChecked: isChecked,
                hasHarzard: hasHarzard,
                socialCreditCode: socialCreditCode, 
                address: address,
                dutyMan: dutyUserName,
                dutyOrgId: searchDutyOrgId,
                dutyOrgIds: searchDutyOrgIds,
                tagIds: tagIds,
                checkerId: searchCheckerId, 
                objectType: objectType, 
                scope: scope,
                scale: scale,
                riskLevel: riskLevel,
                industryType: industryType,
                createStartDt: createStartDt,
                createEndDt: createEndDt,
                lastCheckStartDt: lastCheckStartDt,
                lastCheckEndDt: lastCheckEndDt,
                isDel: isDel,
                includeTags: true
                );

            // 应用 OR scope：checkerId OR dutyOrg（含子孙组织）
            if (useOrScope)
            {
                var orgRootIds = new List<long>();
                if (dutyOrgId.HasValue && dutyOrgId.Value > 0) orgRootIds.Add(dutyOrgId.Value);
                if (dutyOrgIds != null) orgRootIds.AddRange(dutyOrgIds);
                var scopeOrgIds = App.DAL.Org.All
                    .GetDescendants(orgRootIds.Distinct().ToList())
                    .Select(o => o.Id)
                    .Distinct()
                    .ToList();
                var uid = effCheckerId.Value;
                q = q.Where(o => o.CheckerId == uid
                              || (o.DutyOrgId.HasValue && scopeOrgIds.Contains(o.DutyOrgId.Value)));
            }

            var cu = Auth.GetUser();
            if (!Auth.IsAdmin(cu))
                q = q.FilterByOrg(cu, nameof(CheckObject.DutyOrgId));
            var list = q.SortPageExport(pi);

            // 本地计算下次巡查时间并过滤（NextCheckDt 是 NotMapped getter，EF 无法翻译为 SQL，必须在内存做）。
            // 注意：SortPageExport 只返回当前页 pi.PageSize 条；但 nextCheck 过滤必须基于 scope 全量数据，
            // 所以这里重新在 scope 全量基础上计算 NextCheckDt，过滤后再手动分页。
            if (nextCheckFrom.HasValue || nextCheckTo.HasValue)
            {
                static DateTime? Compute(DateTime? lastCheckDt, CheckRiskLevel? riskLv)
                {
                    if (!lastCheckDt.HasValue) return null;
                    var months = riskLv switch
                    {
                        CheckRiskLevel.None   => 12,
                        CheckRiskLevel.Low    => 9,
                        CheckRiskLevel.Medium => 6,
                        CheckRiskLevel.High   => 3,
                        _                    => 12
                    };
                    return lastCheckDt.Value.AddMonths(months);
                }

                var fullProjection = q
                    .Select(o => new { o.Id, o.LastCheckDt, o.RiskLevel })
                    .AsEnumerable()
                    .DistinctBy(r => r.Id)
                    .Where(r =>
                    {
                        var nxt = Compute(r.LastCheckDt, r.RiskLevel);
                        if (nextCheckFrom.HasValue && (!nxt.HasValue || nxt.Value < nextCheckFrom.Value)) return false;
                        if (nextCheckTo.HasValue   && (!nxt.HasValue || nxt.Value > nextCheckTo.Value))   return false;
                        return true;
                    })
                    .Select(r => r.Id)
                    .Distinct()
                    .ToList();
                pi.SetTotal(fullProjection.Count);
                var pageIds = fullProjection
                    .Skip(pi.PageIndex * pi.PageSize)
                    .Take(pi.PageSize)
                    .ToList();
                var pageRows = q
                    .Where(o => pageIds.Contains(o.Id))
                    .SortPageExport(pi);
                pi.SetTotal(fullProjection.Count);
                return BuildResult(0, "success", pageRows, pi);
            }

            return BuildResult(0, "success", list, pi);
        }

        //-------------------------------------------------------------------------
        // 导出
        //-------------------------------------------------------------------------
        public IActionResult OnPostExport(Paging pi, 
            string name="", 
            string code="",
            string socialCreditCode="", 
            string address="",
            string dutyUserName="",
            bool? hasHarzard=null,
            bool? isChecked=null,
            long? dutyOrgId=null,
            List<long> dutyOrgIds=null,
            long? checkerId=null,
            long? checkId=null, 
            CheckObjectType? objectType=null, 
            CheckScope? scope=null,
            CheckObjectScale? scale=null, 
            CheckRiskLevel? riskLevel=null,
            CheckIndustryType? industryType=null,
            List<DateTime> createDt=null,
            List<DateTime> lastCheckDt=null,
            List<long> tagIds=null,
            bool? isDel=null)
        {
            tagIds = ParseTagIds(tagIds, Request.Form["tagIds"]);
            DateTime? createStartDt = createDt.GetVal(0);
            DateTime? createEndDt = createDt.GetVal(1);
            DateTime? lastCheckStartDt = lastCheckDt.GetVal(0);
            DateTime? lastCheckEndDt = lastCheckDt.GetVal(1);
            var effCheckerId = checkId ?? checkerId;
            var exportPi = new Paging { PageIndex = 1, PageSize = int.MaxValue, SortField = pi.SortField, SortDirection = pi.SortDirection };
            var q = CheckObject.Search(
                name: name, 
                code: code,
                isChecked: isChecked,
                hasHarzard: hasHarzard,
                socialCreditCode: socialCreditCode, 
                address: address,
                dutyMan: dutyUserName,
                dutyOrgId: dutyOrgId,
                dutyOrgIds: dutyOrgIds,
                tagIds: tagIds,
                checkerId: effCheckerId, 
                objectType: objectType, 
                scope: scope,
                scale: scale,
                riskLevel: riskLevel,
                industryType: industryType,
                createStartDt: createStartDt,
                createEndDt: createEndDt,
                lastCheckStartDt: lastCheckStartDt,
                lastCheckEndDt: lastCheckEndDt,
                isDel: isDel,
                includeTags: true
                );
            var cuExport = Auth.GetUser();
            if (!Auth.IsAdmin(cuExport))
                q = q.FilterByOrg(cuExport, nameof(CheckObject.DutyOrgId));

            var list = q.SortPageExport(exportPi);
            ExcelExporter.Export(list, $"检查对象列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            Logger.Info($"导出检查对象列表，共 {list.Count} 条记录");
            return new EmptyResult();
        }


        //-------------------------------------------------------------------------
        // 删除
        //-------------------------------------------------------------------------
        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckObjectDelete))
                return BuildResult(403, "无权操作");

            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);
            var scopeId = cu?.EffectiveAuthOrgId;
            foreach (var id in ids)
            {
                var item = CheckObject.Get(id);
                if (item == null) continue;
                if (!isAdmin && !OrgFilter.IsAuth(scopeId, item.DutyOrgId))
                    return BuildResult(403, $"无权删除记录（Id={id}）");
                item.Delete();
            }
            return BuildResult(0, "删除成功");
        }

        //-------------------------------------------------------------------------
        // 导入
        //-------------------------------------------------------------------------
        public IActionResult OnPostImport()
        {
            if (!CheckPower(Power.CheckObjectEdit))
                return BuildResult(403, "无权操作");

            var url = "/Shared/Importor?type=" + Uri.EscapeDataString("App.DAL.CheckObject");
            return EleHandler.ShowDrawer(
                title: "导入检查对象",
                url: url,
                //size: "980px",
                direction: "rtl",
                closeAction: DrawerCloseAction.RefreshData);
        }

        

        //-------------------------------------------------------------------------
        // 批量修改（请求体 EditBatchRequest 由 EleUI 统一提供）
        //-------------------------------------------------------------------------

        /// <summary>批量修改检查对象字段（空字段不覆盖）</summary>
        public IActionResult OnPostEditBatch([FromBody] EditBatchRequest req)
        {
            var ids = req?.Ids ?? Array.Empty<long>();
            var fields = req?.Fields ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var r = BatchUpdater.Update<CheckObject>(
                ids: ids,
                fields: fields,
                requirePower: Power.CheckObjectEdit,
                logTitle: "检查对象批量修改");
            return BuildResult(r.Code, r.Message, r.Data);
        }

        //-------------------------------------------------------------------------
        // Fix data
        //-------------------------------------------------------------------------
        public IActionResult OnPostFixData()
        {
            if (!CheckPower(Power.AdminMaintain))
                return BuildResult(403, "无权操作");

            var cnt = CheckObject.FixAll();
            // 显示toast、刷新数据
            return EleHandler.ShowToast($"共更新 {cnt} 条记录");
        }
    }
}
