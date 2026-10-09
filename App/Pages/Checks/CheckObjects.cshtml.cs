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
        public CheckObject Item { get; set; } = new CheckObject();
        public List<long> DutyOrgIds { get; set; } = new List<long>();
        public long? DefaultCheckerId { get; set; }
        public string DefaultCheckerName { get; set; }

        /// <summary>dutyOrgId（单值）从 URL 注入的原始值，用于 ElePicker/Select 控件单独匹配</summary>
        public long? DutyOrgId { get; set; }

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
        }

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
            bool? isDel=null
            )
        {
            tagIds = ParseTagIds(tagIds, Request.Query["tagIds"]);
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
                var parts = rawNext.Split(',', StringSplitOptions.RemoveEmptyEntries);
                // 约定：URL 传两个日期时，[from, to] 都是闭区间（含端点当日），按卡片范围 WorkDesk 定义：
                //   临期 nextCheck={today+1},{today+7}  =>  from=today+1.Date to=today+7.Date.EndOfDay
                //   超期 nextCheck=,{today}              =>  from=null      to=today.Date.EndOfDay（闭区间，NextCheck<=today）
                if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var d0)) nextCheckFrom = d0.Date;
                if (parts.Length >= 2 && DateTime.TryParse(parts[1], out var d1)) nextCheckTo = d1.Date.AddDays(1).AddTicks(-1);
            }

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

            var list = q.SortPageExport(exportPi);
            ExcelExporter.Export(list, $"检查对象列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            Logger.Info($"导出检查对象列表，共 {list.Count} 条记录");
            return new EmptyResult();
        }


        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckObjectDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                var item = CheckObject.Get(id);
                if (item != null)
                    item.Delete();
            }
            return BuildResult(0, "删除成功");
        }

        public record BatchUpdateRequest(long[] Ids, Dictionary<string, object> Fields);

        /// <summary>批量修改检查对象字段（空字段不覆盖）</summary>
        public IActionResult OnPostBatchSave([FromBody] BatchUpdateRequest req)
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
    }
}
