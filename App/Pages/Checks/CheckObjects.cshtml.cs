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

namespace App.Pages.Checks
{
    [Auth(Power.CheckObjectView)]
    public class CheckObjectsModel : AdminModel
    {
        public CheckObject Item { get; set; }

        public void OnGet() { }

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
            DateTime? createStartDt=null,
            DateTime? createEndDt=null,
            DateTime? updateStartDt=null,
            DateTime? updateEndDt=null,
            DateTime? latestCheckStartDt=null,
            DateTime? latestCheckEndDt=null,
            List<long> tagIds=null,
            bool? isDel=null
            )
        {
            var q = CheckObject.Search(
                name: name, 
                code: code,
                isChecked: isChecked,
                hasHarzard: hasHarzard,
                socialCreditCode: socialCreditCode, 
                address: address,
                dutyUserName: dutyUserName,
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
                updateStartDt: updateStartDt,
                updateEndDt: updateEndDt,
                latestCheckStartDt: latestCheckStartDt,
                latestCheckEndDt: latestCheckEndDt,
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
            long? checkerId=null, 
            CheckObjectType? objectType=null, 
            CheckScope? scope=null,
            CheckObjectScale? scale=null, 
            CheckRiskLevel? riskLevel=null,
            CheckIndustryType? industryType=null,
            DateTime? createStartDt=null,
            DateTime? createEndDt=null,
            DateTime? updateStartDt=null,
            DateTime? updateEndDt=null,
            DateTime? latestCheckStartDt=null,
            DateTime? latestCheckEndDt=null,
            List<long> tagIds=null,
            bool? isDel=null)
        {
            var exportPi = new Paging { PageIndex = 1, PageSize = int.MaxValue, SortField = pi.SortField, SortDirection = pi.SortDirection }; // 导出所有匹配的数据（不分页）,保持与页面上相同的排序
            var q = CheckObject.Search(
                name: name, 
                code: code,
                isChecked: isChecked,
                hasHarzard: hasHarzard,
                socialCreditCode: socialCreditCode, 
                address: address,
                dutyUserName: dutyUserName,
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
                updateStartDt: updateStartDt,
                updateEndDt: updateEndDt,
                latestCheckStartDt: latestCheckStartDt,
                latestCheckEndDt: latestCheckEndDt,
                isDel: isDel,
                includeTags: true
                );

            var list = q.SortPageExport(exportPi);
            ExcelExporter.Export(list, $"检查对象列表_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
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

        public IActionResult OnPostImport()
        {
            if (!CheckPower(Power.CheckObjectEdit))
                return BuildResult(403, "无权操作");

            var url = "/Shared/Importor?type=" + Uri.EscapeDataString("App.DAL.CheckObject");
            return EleManager.ShowDrawer(
                title: "导入检查对象",
                url: url,
                //size: "980px",
                direction: "rtl",
                closeAction: DrawerCloseAction.RefreshData);
        }
    }
}
