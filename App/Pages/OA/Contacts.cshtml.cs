using System;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.EleUI;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.OA
{
    /// <summary>联系人管理页</summary>
    [Auth(Power.ContactView)]
    public class ContactsModel : AdminModel
    {
        public Contact Item { get; set; }

        /// <summary>加载页面</summary>
        public void OnGet()
        {
        }

        /// <summary>获取联系人列表</summary>
        public IActionResult OnGetData(Paging pi, string name = null, string tel = null, long? orgId = null, long? menuId = null, string title = null)
        {
            var list = Contact.Search(name, tel, orgId, menuId, title).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        /// <summary>删除联系人</summary>
        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.ContactDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                var item = Contact.Get(id);
                item?.Delete();
            }
            return BuildResult(0, "删除成功");
        }

        /// <summary>打开导入窗口</summary>
        public IActionResult OnPostImport()
        {
            if (!CheckPower(Power.ContactNew))
                return BuildResult(403, "无权操作");

            var url = "/Shared/Importor?type=" + Uri.EscapeDataString("App.DAL.OA.Contact") + "&ignoreId=true";
            return EleManager.ShowDrawer(
                title: "导入联系人",
                url: url,
                direction: "rtl",
                closeAction: DrawerCloseAction.RefreshData);
        }
    }
}
