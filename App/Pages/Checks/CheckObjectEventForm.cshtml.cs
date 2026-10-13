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
    [Auth(Power.CheckObjectEdit)]
    public class CheckObjectEventFormModel : AuthModel
    {
        public long ObjectId { get; set; }
        public string ObjectName { get; set; }
        public string ObjectDisplay { get; set; }
        public CheckObjectEvent Item { get; set; }

        public void OnGet(long objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            if (ObjectName.IsNotEmpty() && ObjectId > 0)
                ObjectName = CheckObject.Get(ObjectId)?.Name ?? string.Empty;

            ObjectDisplay = ObjectName.IsEmpty() ? $"ID:{ObjectId}" : $"{ObjectName} (ID:{ObjectId})";
        }


        public IActionResult OnGetData(long id, long objectId = 0)
        {
            // 编辑/查看时 objectId 可能未传入，从事件记录本身获取
            if (id > 0)
            {
                var item = CheckObjectEvent.Get(id);
                if (item == null)
                    return BuildResult(404, "对象事件不存在");

                var objId = objectId > 0 ? objectId : item.CheckObjectId;
                var objectName = CheckObject.Get(objId)?.Name;
                return BuildResult(0, "success", BuildFormData(item, objId, objectName));
            }
            else            
            {
                // 新增时必须有 objectId
                if (objectId <= 0)
                    return BuildResult(400, "参数错误：缺少检查对象ID");
                var name = CheckObject.Get(objectId)?.Name;
                return BuildResult(0, "success", BuildFormData(new CheckObjectEvent(), objectId, name));                
            }
        }

        /// <summary>构建表单数据</summary>
        private object BuildFormData(CheckObjectEvent item, long objectId, string objectName)
        {
            var display = ObjectName.IsNotEmpty() ? $"ID:{objectId}" : $"{objectName} (ID:{objectId})";
            return new
            {
                item.Id,
                item.Type,
                item.Title,
                item.Content,
                item.MainImage,
                item.HappenDt,
                objectId = objectId,
                objectName = objectName,
                objectDisplay = display
            };
        }

        public IActionResult OnPostSave([FromBody] CheckObjectEvent req, long objectId)
        {
            var checkObject = CheckObject.Get(objectId);
            if (req == null)           return BuildResult(400, "参数错误");
            if (objectId <= 0)         return BuildResult(400, "参数错误：缺少检查对象ID");
            if (checkObject == null)   return BuildResult(404, "检查对象不存在");

            CheckObjectEvent item = (req.Id > 0) ? CheckObjectEvent.GetDetail(req.Id) : new CheckObjectEvent { CheckObjectId = objectId};
            item.Title = req.Title;
            item.Content = req.Content;
            item.HappenDt = req.HappenDt;
            item.MainImage = Uploader.SaveFile(nameof(CheckObjectEvent), req.MainImage);
            item.Save();

            return BuildResult(0, "保存成功");
        }
    }
}
