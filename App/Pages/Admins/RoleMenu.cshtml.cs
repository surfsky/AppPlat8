using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Admins
{
    [Auth(Power.RolePowerEdit)]
    public class RoleMenuModel : AdminModel
    {
        public long RoleId { get; set; }

        public void OnGet(long roleId)
        {
            RoleId = roleId;
        }

        public IActionResult OnGetMenus()
        {
            return BuildResult(0, "success", Menu.GetTree());
        }

        public IActionResult OnGetRoleMenuIds(long roleId)
        {
            var ids = RoleMenu.GetRoleMenuIds(roleId);
            return BuildResult(0, "success", ids);
        }

        public IActionResult OnPostSaveRoleMenus([FromBody] SaveRoleMenuRequest req)
        {
            if (req == null || req.RoleId <= 0)
                return BuildResult(400, "参数错误");

            RoleMenu.SetRoleMenus(req.RoleId, req.MenuIds ?? new List<long>());
            return BuildResult(0, "保存成功");
        }

        public class SaveRoleMenuRequest
        {
            public long RoleId { get; set; }
            public List<long> MenuIds { get; set; }
        }
    }
}
