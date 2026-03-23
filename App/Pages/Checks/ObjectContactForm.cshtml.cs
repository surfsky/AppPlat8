using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckObjectEdit)]
    public class ObjectContactFormModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectDisplay { get; set; }

        public CheckObjectContact Item { get; set; }

        public void OnGet(long objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            if (string.IsNullOrWhiteSpace(ObjectName) && ObjectId > 0)
            {
                ObjectName = CheckObject.Get(ObjectId)?.Name ?? string.Empty;
            }
        }

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
                var exists = CheckObjectContact.IncludeSet.Any(o => o.Id == id && o.CheckObject.Id == objectId);
                if (!exists)
                    return BuildResult(404, "联系人不存在");

                var item = CheckObjectContact.Get(id);
                return BuildResult(0, "success", BuildFormData(item, objectId, objectName));
            }

            return BuildResult(0, "success", BuildFormData(new CheckObjectContact(), objectId, objectName));
        }

        public IActionResult OnPostSave([FromBody] CheckObjectContact req, long objectId)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");

            var checkObject = CheckObject.Get(objectId);
            if (checkObject == null)
                return BuildResult(404, "检查对象不存在");

            CheckObjectContact item;
            if (req.Id > 0)
            {
                var exists = CheckObjectContact.IncludeSet.Any(o => o.Id == req.Id && o.CheckObject.Id == objectId);
                if (!exists)
                    return BuildResult(403, "无权操作该联系人");

                item = CheckObjectContact.Get(req.Id);
            }
            else
            {
                item = new CheckObjectContact
                {
                    CheckObject = checkObject
                };
            }

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
