using System;
using System.Collections.Generic;
using System.Linq;
using App.DAL;
using App.Pages;
using App.Utils;
using App.Web;
using App.HttpApi; // Added for APIResult
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Maintains
{
    public class MenusModel : BaseModel
    {        
        public Menu Item { get; set; }

        public void OnGet() { }


        /// <summary>获取菜单树</summary>
        public IActionResult OnGetData()
        {
            var menus = Menu.GetTree();
            return BuildResult(0, "success", menus);
        }

        /// <summary>删除菜单</summary>
        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0) return BuildResult(400, "请选择要删除的记录");
            foreach (var id in ids)
            {
                var item = Menu.Get(id);
                if (item != null)
                {
                    // 检查是否有子菜单
                    if (Menu.Set.Any(m => m.ParentId == id))
                        return BuildResult(400, $"菜单[{item.Name}]下还有子菜单，请先删除子菜单");

                    item.Delete();
                }
            }

            Menu.ClearCache(); // 刷新缓存
            return BuildResult(0, "删除成功");
        }
    }
}
