using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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
        public long? DefaultCheckerId { get; set; }      // 默认检查人ID
        public string DefaultCheckerName { get; set; }  // 默认检查人名称
        public long? DutyOrgId { get; set; }  // 单值，用于 ElePicker/Select 控件单独匹配
        public string ScopeTagIds { get; set; }  // 多值，用于标签树控件回显

        
        //-------------------------------------------------------------------------
        // 输入参数控制
        //-------------------------------------------------------------------------
        public void OnGet()
        {
            var user = GetUser();

            // DutyOrgId
            var dutyOrgId = Asp.GetQueryLong("dutyOrgId");
            if (dutyOrgId != null)
            {
                DutyOrgId = dutyOrgId;
                Item.DutyOrgId = dutyOrgId;
            }
            else
            {
                DutyOrgId = user.AuthOrgId;
                Item.DutyOrgId = user.AuthOrgId;
            }

            // CheckerId
            var checkerId = Asp.GetQueryLong("checkerId");
            if (checkerId != null)
            {
                DefaultCheckerId = checkerId.Value;
                Item.CheckerId = checkerId.Value;
                var u = App.DAL.User.Get(checkerId.Value);
                if (u != null)
                    DefaultCheckerName = u.DisplayName;
            }
            else
            {
                DefaultCheckerId = user.Id;
                Item.CheckerId = user.Id;
                DefaultCheckerName = user.DisplayName;
            }

            // tagName
            var tagName = Asp.GetQueryString("tagName");
            if (tagName.IsNotEmpty())
            {
                var tag = CheckTag.Set.FirstOrDefault(t => t.Name == tagName);
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


        //-------------------------------------------------------------------------
        // 查询 & 导出
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
            long? checkerId=null,
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
            var q = Query(name, code, socialCreditCode, address, dutyUserName, hasHarzard, isChecked, dutyOrgId, checkerId, objectType, scope, scale, riskLevel, industryType, createDt, lastCheckDt, tagIds, tagName, isDel);
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }


        // 导出
        public IActionResult OnPostExport(
            Paging pi,
            string name="", 
            string code="",
            string socialCreditCode="", 
            string address="",
            string dutyUserName="",
            bool? hasHarzard=null,
            bool? isChecked=null,
            long? dutyOrgId=null,
            long? checkerId=null,
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
            var q = Query(name, code, socialCreditCode, address, dutyUserName, hasHarzard, isChecked, dutyOrgId, checkerId, objectType, scope, scale, riskLevel, industryType, createDt, lastCheckDt, tagIds, tagName, isDel);
            var exportPi = new Paging { PageIndex = 1, PageSize = int.MaxValue, SortField = pi.SortField, SortDirection = pi.SortDirection };
            var list = q.SortPageExport(exportPi);
            ExcelExporter.Export(list, $"检查对象列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            Logger.Info($"导出检查对象列表，共 {list.Count} 条记录");
            return new EmptyResult();
        }

        // 查询
        private IQueryable<CheckObject> Query(
            string name="", 
            string code="",
            string socialCreditCode="", 
            string address="",
            string dutyUserName="",
            bool? hasHarzard=null,
            bool? isChecked=null,
            long? dutyOrgId=null, 
            long? checkerId=null,
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
            // tagIds
            tagIds = ParseTagIds(tagIds, Request.Query["tagIds"]);
            if (tagIds == null || tagIds.Count == 0)
            {
                var rawTagName = tagName.IsEmpty() ? Request.Query["tagName"].FirstOrDefault() : tagName;
                if (rawTagName.IsNotEmpty())
                {
                    var tag = CheckTag.Set.FirstOrDefault(t => t.Name == rawTagName);
                    if (tag != null) tagIds = new List<long> { tag.Id };
                }
            }

            // date
            DateTime? createStartDt = createDt.GetVal(0);
            DateTime? createEndDt = createDt.GetVal(1);
            DateTime? lastCheckStartDt = lastCheckDt.GetVal(0);
            DateTime? lastCheckEndDt = lastCheckDt.GetVal(1);
            DateTime? nextCheckFrom = null;
            DateTime? nextCheckTo = null;
            var rawNext = Request.Query["nextCheck"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawNext))
            {
                // 不能用 RemoveEmptyEntries，否则 ",2026-10-14" 会丢失空的 from，导致 to 被当成 from
                // 约定：URL 传两个日期时，[from, to] 都是闭区间（含端点当日），按卡片范围 WorkDesk 定义：
                //   临期 nextCheck={today+1},{today+7}  =>  from=today+1.Date to=today+7.Date.EndOfDay
                //   超期 nextCheck=,{today}              =>  from=null      to=today.Date.EndOfDay（闭区间，NextCheck<=today）
                var parts = rawNext.Split(',');
                if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var d0)) nextCheckFrom = d0.Date;
                if (parts.Length >= 2 && DateTime.TryParse(parts[1], out var d1)) nextCheckTo = d1.Date.AddDays(1).AddTicks(-1);
            }

            // query
            var q = CheckObject.Search(
                name: name,
                code: code,
                isChecked: isChecked,
                hasHarzard: hasHarzard,
                socialCreditCode: socialCreditCode,
                address: address,
                dutyMan: dutyUserName,
                dutyOrgId: dutyOrgId,
                tagIds: tagIds,
                checkerId: checkerId,
                objectType: objectType,
                scope: scope,
                scale: scale,
                riskLevel: riskLevel,
                industryType: industryType,
                createStartDt: createStartDt,
                createEndDt: createEndDt,
                lastCheckStartDt: lastCheckStartDt,
                lastCheckEndDt: lastCheckEndDt,
                nextCheckFrom: nextCheckFrom,
                nextCheckTo: nextCheckTo,
                isDel: isDel,
                includeTags: true
                );


            // 应用组织过滤
            var cu = Auth.GetUser();
            if (!Auth.IsAdmin(cu))
                q = q.FilterByOrg(cu, nameof(CheckObject.DutyOrgId));
            return q;
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
            return BuildResult(0, $"修正数据完成，共修正{cnt}条记录。修改字段：IsChecked, NextCheckDt。");
        }
    }
}
