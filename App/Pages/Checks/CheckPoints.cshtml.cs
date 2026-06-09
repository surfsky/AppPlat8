using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.EleUI;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckObjectView)]
    public class CheckPointsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        public CheckPoint Item { get; set; }

        public void OnGet(long objectId, string objectName)
        {
            ObjectId = objectId;
            if (string.IsNullOrWhiteSpace(objectName))
                ObjectName = CheckObject.Get(objectId)?.Name ?? string.Empty;
            else
                ObjectName = objectName;
        }

        public IActionResult OnGetData(Paging pi, long objectId, string name, CheckRiskLevel? riskLevel)
        {
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");

            var list = CheckPoint.Search(objectId: objectId, name: name, riskLevel: riskLevel).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids, long objectId)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");
            if (!CheckPower(Power.CheckObjectEdit))
                return BuildResult(403, "无权操作");

            var items = CheckPoint.IncludeSet
                .Where(o => ids.Contains(o.Id) && o.CheckObjectId == objectId)
                .ToList();

            foreach (var item in items)
                item.Delete();

            return BuildResult(0, "删除成功");
        }

        public IActionResult OnPostImport()
        {
            if (!CheckPower(Power.CheckPointEdit))
                return BuildResult(403, "无权操作");

            var url = "/Shared/Importor?type=" + Uri.EscapeDataString("App.DAL.CheckPoint") + "&objectId=" + ObjectId;
            return EleManager.ShowDrawer(
                title: "导入检查点",
                url: url,
                //size: "980px",
                direction: "rtl",
                closeAction: DrawerCloseAction.RefreshData);
        }

    }
}
