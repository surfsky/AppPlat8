using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.OA
{
    /// <summary>联系人目录表单页</summary>
    [Auth(Power.ContactMenuView)]
    public class ContactMenuFormModel : AdminModel
    {
        public ContactMenu Item { get; set; }

        /// <summary>加载页面</summary>
        public void OnGet(long? id)
        {
        }

        /// <summary>获取目录表单数据</summary>
        public IActionResult OnGetData(long id, long? selectId)
        {
            var item = ContactMenu.GetDetail(id) ?? new ContactMenu();
            if (id == 0)
                item.ParentId = selectId;
            return BuildResult(0, "success", item.Export(ExportMode.Detail));
        }

        /// <summary>保存目录</summary>
        public IActionResult OnPostSave([FromBody] ContactMenu req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var needPower = req.Id > 0 ? Power.ContactMenuEdit : Power.ContactMenuNew;
            if (!CheckPower(needPower))
                return BuildResult(403, "无权操作");

            ContactMenu item;
            if (req.Id > 0)
            {
                item = ContactMenu.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "目录不存在");
                if (req.ParentId == req.Id)
                    return BuildResult(400, "上级目录不能是自己");
            }
            else
            {
                item = new ContactMenu();
            }

            item.Name = req.Name?.Trim();
            item.ParentId = req.ParentId;
            item.SortId = req.SortId;
            item.Save();

            ContactMenu.ClearCache();
            return BuildResult(0, "保存成功");
        }
    }
}
