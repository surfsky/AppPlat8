using App.DAL;
using App.Utils;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace App.Pages.Admins
{
    [Auth(Power.RoleEdit)]
    public class RoleFormModel : AuthModel
    {
        public Role Item { get; set; }

        public void OnGet(long id)
        {
        }

        public IActionResult OnGetData(long id)
        {
            var item = id > 0 ? Role.Get(id) : new Role();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] Role req)
        {
            if (req == null)           return BuildResult(400, "参数错误");
            if (req.Name.IsEmpty())    return BuildResult(400, "角色名称不能为空");

            if (req.Id == 0)
            {
                if (Role.Set.Any(r => r.Name == req.Name))
                    return BuildResult(400, "角色名已存在");
                var item = new Role
                {
                    Name = req.Name.Trim(),
                    Remark = req.Remark,
                    SortId = req.SortId,
                };
                item.Save();
            }
            else
            {
                var role = Role.Get(req.Id);
                role.Name = req.Name.Trim();
                role.Remark = req.Remark;
                role.SortId = req.SortId;
                role.Save();
            }
            return BuildResult(0, "保存成功");
        }
    }
}
