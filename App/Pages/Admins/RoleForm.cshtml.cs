using App.DAL;
using App.Utils;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace App.Pages.Admins
{
    [CheckPower(Power.RoleEdit)]
    public class RoleFormModel : AdminModel
    {
        [BindProperty]
        public Role Item { get; set; }

        public void OnGet(long id)
        {
            if (id > 0)
            {
                Item = Role.Get(id);
            }
            else
            {
                Item = new Role();
            }
        }

        public IActionResult OnGetData(long id)
        {
            var item = id > 0 ? Role.Get(id) : new Role();
            return new JsonResult(new { code = 0, msg = "success", data = item });
        }

        public IActionResult OnPost()
        {
            if (Item.Id == 0)
            {
                if (Role.Set.Any(r => r.Name == Item.Name))
                    return BuildResult(400, "角色名已存在");
                
                Item.Save();
            }
            else
            {
                var existing = Role.Get(Item.Id);
                existing.Name = Item.Name;
                existing.Remark = Item.Remark;
                existing.Save();
            }
            return BuildResult(0, "保存成功");
        }
    }
}
