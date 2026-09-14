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
    public class CheckObjectsModel : AdminModel
    {
        public CheckObject Item { get; set; } = new CheckObject();
        public List<long> DutyOrgIds { get; set; } = new List<long>();
        public long? DefaultCheckerId { get; set; }
        public string DefaultCheckerName { get; set; }

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
            return list.Distinct().OrderBy(t => t).ToList();
        }

        public void OnGet()
        {
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
            DateTime? createStartDt = createDt.GetVal(0);
            DateTime? createEndDt = createDt.GetVal(1);
            DateTime? lastCheckStartDt = lastCheckDt.GetVal(0);
            DateTime? lastCheckEndDt = lastCheckDt.GetVal(1);
            var effCheckerId = checkId ?? checkerId;
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
