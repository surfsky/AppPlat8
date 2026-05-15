using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.HttpApi;
using App.Utils;
using App.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using App.EleUI;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckObjectView)]
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
            string hasHazard="",
            string patrolStatus="",
            long? orgId=null, 
            long? tagId=null,
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
            bool? isDel=null,
            bool? isDemonstration=null,
            bool? isKeySupervision=null,
            bool? isProductInNight=null,
            bool? isThreePlacesThreeEnterprises=null
            )
        {
            bool? hasHazardValue = null;
            if (bool.TryParse(hasHazard, out var parsedHasHazard))
                hasHazardValue = parsedHasHazard;

            bool? hasPatrolValue = null;
            if (patrolStatus == "1") hasPatrolValue = true;
            if (patrolStatus == "0") hasPatrolValue = false;

            var list = CheckObject.Search(
                name, 
                code,
                socialCreditCode, 
                address,
                dutyUserName,
                orgId, 
                tagId,
                checkerId, 
                objectType, 
                scope,
                scale,
                riskLevel,
                industryType,
                createStartDt,
                createEndDt,
                updateStartDt,
                updateEndDt,
                latestCheckStartDt,
                latestCheckEndDt,
                hasHazardValue,
                hasPatrolValue,
                isDel,
                isDemonstration,
                isKeySupervision,
                isProductInNight,
                isThreePlacesThreeEnterprises
                ).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
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

        public IActionResult OnPostOpenCheckObjectImport()
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
