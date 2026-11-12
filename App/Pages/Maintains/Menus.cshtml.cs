using System;
using System.Collections.Generic;
using System.Linq;
using App.DAL;
using App.Pages;
using App.Utils;
using App.Web;
using App.HttpApi; // Added for APIResult
using App.EleUI;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Maintains
{
    public class MenusModel : BaseModel // AuthModel
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

        public IActionResult OnPostMoveUp([FromBody] ElePayload payload) => MoveMenu(payload, -1);

        public IActionResult OnPostMoveDown([FromBody] ElePayload payload) => MoveMenu(payload, 1);

        private IActionResult MoveMenu(ElePayload payload, int direction)
        {
            if (payload?.SelectedCount != 1 || payload.FirstSelectedId is not long id || id <= 0)
                return BuildResult(400, "请只选择一个菜单");

            var selected = Menu.Get(id);
            if (selected == null)
                return BuildResult(404, "菜单不存在");

            var oldSortId = selected.SortId;
            selected = direction < 0 ? selected.MoveUp() : selected.MoveDown();
            if (selected.SortId == oldSortId)
                return BuildResult(400, direction < 0 ? "已经是最上面的菜单" : "已经是最下面的菜单");

            Menu.ClearCache();
            return BuildResult(0, direction < 0 ? "上移成功" : "下移成功", new
            {
                reload = true,
                id = selected.Id
            });
        }
    }
}
