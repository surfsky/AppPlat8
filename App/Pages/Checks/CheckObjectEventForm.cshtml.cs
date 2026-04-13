using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckObjectEdit)]
    public class CheckObjectEventFormModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectDisplay { get; set; }

        public CheckObjectEvent Item { get; set; }
        public List<SelectListItem> TypeItems { get; set; }

        public void OnGet(long objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            if (string.IsNullOrWhiteSpace(ObjectName) && ObjectId > 0)
                ObjectName = CheckObject.Get(ObjectId)?.Name ?? string.Empty;

            ObjectDisplay = string.IsNullOrWhiteSpace(ObjectName) ? $"ID:{ObjectId}" : $"{ObjectName} (ID:{ObjectId})";
            TypeItems = BuildTypeItems();
        }

        private object BuildFormData(CheckObjectEvent item, long objectId, string objectName)
        {
            var display = string.IsNullOrWhiteSpace(objectName) ? $"ID:{objectId}" : $"{objectName} (ID:{objectId})";
            return new
            {
                id = item.Id,
                type = item.Type,
                title = item.Title,
                content = item.Content,
                mainImage = item.MainImage,
                triggleDt = item.TriggleDt,
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
                var exists = CheckObjectEvent.IncludeSet.Any(o => o.Id == id && o.CheckObjectId == objectId);
                if (!exists)
                    return BuildResult(404, "对象事件不存在");

                var item = CheckObjectEvent.Get(id);
                return BuildResult(0, "success", BuildFormData(item, objectId, objectName));
            }

            return BuildResult(0, "success", BuildFormData(new CheckObjectEvent(), objectId, objectName));
        }

        public IActionResult OnPostSave([FromBody] CheckObjectEvent req, long objectId)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");

            var checkObject = CheckObject.Get(objectId);
            if (checkObject == null)
                return BuildResult(404, "检查对象不存在");

            CheckObjectEvent item;
            if (req.Id > 0)
            {
                var exists = CheckObjectEvent.IncludeSet.Any(o => o.Id == req.Id && o.CheckObjectId == objectId);
                if (!exists)
                    return BuildResult(403, "无权操作该对象事件");

                item = CheckObjectEvent.Get(req.Id);
            }
            else
            {
                item = new CheckObjectEvent
                {
                    CheckObjectId = objectId
                };
            }

            item.Type = req.Type;
            item.Title = req.Title;
            item.Content = req.Content;
            item.TriggleDt = req.TriggleDt;
            item.MainImage = Uploader.SaveFile(nameof(CheckObjectEvent), req.MainImage);
            item.Save();

            return BuildResult(0, "保存成功");
        }

        private static List<SelectListItem> BuildTypeItems()
        {
            return Enum.GetValues<CheckObjectEventType>()
                .Select(t => new SelectListItem(t.GetTitle(), ((int)t).ToString()))
                .ToList();
        }
    }
}
