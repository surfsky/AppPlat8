using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [Auth(Power.CheckObjectEdit)]
    public class ObjectContactFormModel : AuthModel
    {
        public long ObjectId { get; set; }
        public string ObjectName { get; set; }
        public CheckObjectContact Item { get; set; }

        // 初始
        public void OnGet(long objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            if (ObjectName.IsEmpty() && ObjectId > 0)
                ObjectName = CheckObject.Get(ObjectId)?.Name;
        }


        // 获取联系人详情
        public IActionResult OnGetData(long id, long objectId)
        {
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");

            var objectName = CheckObject.Get(objectId)?.Name;
            if (id > 0)
            {
                var item = CheckObjectContact.GetDetail(id);
                return BuildResult(0, "success", BuildFormData(item, objectId, objectName));
            }

            return BuildResult(0, "success", BuildFormData(new CheckObjectContact(), objectId, objectName));
        }

        //
        private object BuildFormData(CheckObjectContact item, long objectId, string objectName)
        {
            var display = string.IsNullOrWhiteSpace(objectName) ? $"ID:{objectId}" : $"{objectName} (ID:{objectId})";

            return new
            {
                id = item.Id,
                name = item.Name,
                photo = item.Photo,
                idCard = item.IdCard,
                idCardImage = item.IdCardImage,
                phone = item.Phone,
                certDt = item.CertDt,
                certExpireDt = item.CertExpireDt,
                objectId = objectId,
                objectName = objectName,
                objectDisplay = display,
                contactType = item.ContactType
            };
        }
        
        /// <summary>保存联系人</summary>
        public IActionResult OnPostSave([FromBody] CheckObjectContact req, long objectId)
        {
            var checkObject = CheckObject.Get(objectId);
            if (req == null)            return BuildResult(400, "参数错误");
            if (objectId <= 0)          return BuildResult(400, "参数错误：缺少检查对象ID");
            if (checkObject == null)    return BuildResult(404, "检查对象不存在");

            CheckObjectContact item = req.Id > 0 ? CheckObjectContact.GetDetail(req.Id) : new CheckObjectContact {CheckObject = checkObject};
            item.ContactType = req.ContactType;
            item.Name = req.Name;
            item.Phone = req.Phone;
            item.IdCard = req.IdCard;
            item.CertDt = req.CertDt;
            item.CertExpireDt = req.CertExpireDt;
            item.Photo = Uploader.SaveFile(nameof(CheckObjectContact), req.Photo);
            item.IdCardImage = Uploader.SaveFile(nameof(CheckObjectContact), req.IdCardImage);
            item.Save();

            return BuildResult(0, "保存成功");
        }
    }
}
