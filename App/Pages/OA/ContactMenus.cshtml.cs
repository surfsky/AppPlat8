using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.OA;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.OA
{
    /// <summary>联系人目录管理页</summary>
    [Auth(Power.ContactMenuView)]
    public class ContactMenusModel : AdminModel
    {
        public ContactMenu Item { get; set; }

        /// <summary>加载页面</summary>
        public void OnGet()
        {
        }

        /// <summary>获取目录树</summary>
        public IActionResult OnGetData()
        {
            var items = ContactMenu.GetTree();
            return BuildResult(0, "success", items.Select(t => t.Export()).ToList());
        }

        /// <summary>删除目录</summary>
        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "请选择要删除的记录");
            if (!CheckPower(Power.ContactMenuDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                var item = ContactMenu.Get(id);
                if (item == null)
                    continue;

                if (ContactMenu.Set.Any(t => t.ParentId == id))
                    return BuildResult(400, $"目录[{item.Name}]下还有子目录，请先删除子目录");

                if (Contact.Set.Any(t => t.MenuId == id))
                    return BuildResult(400, $"目录[{item.Name}]下还有联系人，请先迁移或删除联系人");

                item.Delete();
            }

            ContactMenu.ClearCache();
            return BuildResult(0, "删除成功");
        }
    }
}
