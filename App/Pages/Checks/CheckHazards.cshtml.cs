using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckHazardView)]
    public class CheckHazardsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long? ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        public CheckHazard Item { get; set; }

        public void OnGet(long? objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;

            if (ObjectId.GetValueOrDefault() > 0 && string.IsNullOrWhiteSpace(ObjectName))
            {
                ObjectName = CheckObject.Get(ObjectId.Value)?.Name ?? string.Empty;
            }
        }

        public async Task<IActionResult> OnGetData(Paging pi, string objectName, long? objectId, string checkerName, long? checkerId, CheckHazardStatus? status, DateTime? createStartDt)
        {
            var list = CheckHazard.Search(objectName, objectId, checkerName, checkerId, status, createStartDt).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckHazardDelete))
                return BuildResult(403, "无权操作");
            foreach (var id in ids)
                CheckHazard.Delete(id);
            return BuildResult(0, "删除成功");
        }

        public IActionResult OnPostSave([FromBody] CheckHazard req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckHazardEdit))
                return BuildResult(403, "无权操作");

            CheckHazard item = req.Id == 0 ? new CheckHazard() : CheckHazard.Get(req.Id);
            item.Status = req.Status;
            item.ExpireDt = req.ExpireDt;
            item.RectifyDt = req.RectifyDt;
            item.IsIn141 = req.IsIn141;
            item.ObjectId = req.ObjectId;
            item.CheckerId = req.CheckerId;
            item.CheckLogId = req.CheckLogId;
            item.CheckSheetId = req.CheckSheetId;
            item.CheckItemId = req.CheckItemId;
            item.CheckItemText = req.CheckItemText;
            item.Description = req.Description;
            item.Save();
                
            return BuildResult(0, "保存成功");
        }
    }
}
