using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.RolePowerEdit)]
    public class RoleGisMenuModel : AdminModel
    {
        public long RoleId { get; set; }

        public void OnGet(long roleId)
        {
            RoleId = roleId;
        }

        public IActionResult OnGetMenus(long roleId)
        {
            var selected = RoleGisMenu.GetRoleMenus(roleId)
                .ToDictionary(t => t.MenuId, t => t);

            var all = GisMenu.GetTree();
            var data = all.Select(t => ToNode(t, selected)).ToList();
            return BuildResult(0, "success", data);
        }

        public IActionResult OnPostSave([FromBody] SaveReq req)
        {
            if (req == null || req.RoleId <= 0)
                return BuildResult(400, "参数错误");

            var items = (req.Items ?? new List<SaveItem>())
                .Select(t => new RoleGisMenu
                {
                    RoleId = req.RoleId,
                    MenuId = t.MenuId,
                    CanSee = t.CanSee,
                    CanEdit = t.CanEdit,
                })
                .ToList();

            RoleGisMenu.SetRoleMenus(req.RoleId, items);
            return BuildResult(0, "保存成功");
        }

        static object ToNode(GisMenu menu, Dictionary<long, RoleGisMenu> selected)
        {
            var canSee = false;
            var canEdit = false;
            if (selected != null && selected.TryGetValue(menu.Id, out var item))
            {
                canSee = item.CanSee;
                canEdit = item.CanEdit;
            }

            return new
            {
                id = menu.Id,
                name = menu.Name,
                canSee,
                canEdit,
                children = (menu.Children ?? new List<GisMenu>()).Select(t => ToNode(t, selected)).ToList()
            };
        }

        public class SaveReq
        {
            public long RoleId { get; set; }
            public List<SaveItem> Items { get; set; }
        }

        public class SaveItem
        {
            public long MenuId { get; set; }
            public bool CanSee { get; set; }
            public bool CanEdit { get; set; }
        }
    }
}
