using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckObjectEdit)]
    public class CheckPointFormModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        public CheckPoint Item { get; set; }

        public void OnGet(long objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            if (string.IsNullOrWhiteSpace(ObjectName) && ObjectId > 0)
                ObjectName = CheckObject.Get(ObjectId)?.Name ?? string.Empty;
        }

        private object BuildFormData(CheckPoint item, long objectId, string objectName)
        {
            var display = string.IsNullOrWhiteSpace(objectName) ? $"ID:{objectId}" : $"{objectName} (ID:{objectId})";

            return new
            {
                id = item.Id,
                name = item.Name,
                riskLevel = item.RiskLevel,
                gps = item.Gps,
                picture = item.Picture,
                objectId = objectId,
                objectName = objectName,
                objectDisplay = display
            };
        }

        public IActionResult OnGetData(long id, long objectId)
        {
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");

            var objectName = CheckObject.Get(objectId)?.Name ?? string.Empty;

            if (id > 0)
            {
                var exists = CheckPoint.IncludeSet.Any(o => o.Id == id && o.CheckObjectId == objectId);
                if (!exists)
                    return BuildResult(404, "风险点不存在");

                var item = CheckPoint.Get(id);
                return BuildResult(0, "success", BuildFormData(item, objectId, objectName));
            }

            return BuildResult(0, "success", BuildFormData(new CheckPoint(), objectId, objectName));
        }

        public IActionResult OnPostSave([FromBody] CheckPoint req, long objectId)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");

            var checkObject = CheckObject.Get(objectId);
            if (checkObject == null)
                return BuildResult(404, "检查对象不存在");

            CheckPoint item;
            if (req.Id > 0)
            {
                var exists = CheckPoint.IncludeSet.Any(o => o.Id == req.Id && o.CheckObjectId == objectId);
                if (!exists)
                    return BuildResult(403, "无权操作该风险点");

                item = CheckPoint.Get(req.Id);
            }
            else
            {
                item = new CheckPoint
                {
                    CheckObjectId = objectId
                };
            }

            item.Name = req.Name;
            item.RiskLevel = req.RiskLevel;
            item.Gps = req.Gps;
            item.Picture = Uploader.SaveFile(nameof(CheckPoint), req.Picture);
            item.Save();

            return BuildResult(0, "保存成功");
        }
    }
}
