using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.OA
{
    /// <summary>联系人表单页</summary>
    [Auth(Power.ContactView)]
    public class ContactFormModel : AdminModel
    {
        public Contact Item { get; set; }

        /// <summary>加载页面</summary>
        public void OnGet()
        {
        }

        /// <summary>获取联系人表单数据</summary>
        public IActionResult OnGetData(long id, long? menuId)
        {
            var item = Contact.GetDetail(id) ?? new Contact();
            if (id == 0 && menuId.HasValue)
                item.MenuId = menuId;
            return BuildResult(0, "success", item.Export(ExportMode.Detail));
        }

        /// <summary>保存联系人</summary>
        public IActionResult OnPostSave([FromBody] Contact req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (string.IsNullOrWhiteSpace(req.Name))
                return BuildResult(400, "姓名不能为空");

            var needPower = req.Id > 0 ? Power.ContactEdit : Power.ContactNew;
            if (!CheckPower(needPower))
                return BuildResult(403, "无权操作");

            Contact item;
            if (req.Id > 0)
            {
                item = Contact.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "联系人不存在");
            }
            else
            {
                item = new Contact();
            }

            item.Name = req.Name?.Trim();
            item.Tel = req.Tel?.Trim();
            item.Title = req.Title?.Trim();
            item.MenuId = req.MenuId;
            item.OrgId = req.OrgId;
            item.JsonData = req.JsonData?.Trim();
            item.Save();

            return BuildResult(0, "保存成功", new { id = item.Id });
        }
    }
}
