using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckHazardEdit)]
    public class CheckHazardFormModel : AdminModel
    {
        public CheckHazard Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(
            long id,
            long? objectId,
            long? checkLogId,
            long? checkSheetId,
            long? checkItemId,
            string checkItemText)
        {
            var item = id > 0 ? (CheckHazard.GetDetail(id) ?? new CheckHazard()) : new CheckHazard();
            if (id <= 0)
            {
                item.ObjectId = objectId;
                item.CheckLogId = checkLogId;
                item.CheckSheetId = checkSheetId;
                item.CheckItemId = checkItemId;
                item.CheckItemText = checkItemText;
            }

            var objectName = item.ObjectName;
            if (string.IsNullOrWhiteSpace(objectName) && item.ObjectId.HasValue)
                objectName = CheckObject.Get(item.ObjectId)?.Name ?? string.Empty;

            var sheetName = item.CheckSheetName;
            if (string.IsNullOrWhiteSpace(sheetName) && item.CheckSheetId.HasValue)
                sheetName = CheckSheet.Get(item.CheckSheetId)?.Name ?? string.Empty;

            var data = new
            {
                item.Id,
                item.ObjectId,
                ObjectName = objectName,
                item.CheckLogId,
                item.CheckSheetId,
                CheckSheetName = sheetName,
                item.CheckItemId,
                item.CheckItemText,
                item.Description,
                item.Status,
                item.ExpireDt,
                item.RectifyDt,
                item.IsIn141,
                ImageUrls = item.ImageUrls
            };

            return BuildResult(0, "success", data);
        }

        public IActionResult OnPostSave([FromBody] CheckHazard req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = CheckHazard.Get(req.Id);
            if (item == null)
            {
                // Typically hazards are created via API from mobile app or check process, not manually created here from scratch usually.
                // But for admin purpose, let's allow basic edit.
                //return BuildResult(404, "隐患不存在");
                item = new CheckHazard();
                item.ObjectId = req.ObjectId;
                item.CheckItemId = req.CheckItemId;
                item.CheckSheetId = req.CheckSheetId;
                item.CheckLogId = req.CheckLogId;
                item.CheckerId = GetUserId();
                item.CheckItemText = req.CheckItemText;
            }

            item.Description = req.Description;
            item.Status = req.Status;
            item.ExpireDt = req.ExpireDt;
            item.RectifyDt = req.RectifyDt;
            item.IsIn141 = req.IsIn141;
            item.Save();

            // 保存图片
            item.AddAtt(Uploader.SaveFiles(nameof(CheckHazard), req.ImageUrls));

            if (item.CheckLogId.HasValue)
            {
                var check = Check.Get(item.CheckLogId.Value);
                if (check == null)
                {
                    var userId = GetUserId();
                    var user = GetUser();
                    check = new Check
                    {
                        Id = item.CheckLogId.Value,
                        CreateDt = DateTime.Now,
                        CheckDt = DateTime.Now,
                        CheckObjectId = item.ObjectId,
                        CheckerId = userId,
                        OrgId = user?.OrgId,
                        HazardCount = 0,
                        RemainHazardCount = 0,
                        Result = false,
                        IsClosed = false
                    };
                    check.Save();
                }
            }

            return BuildResult(0, "保存成功");
        }

    }
}
