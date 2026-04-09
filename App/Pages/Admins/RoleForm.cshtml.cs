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

        public IActionResult OnPostSave([FromBody] Role req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            if (string.IsNullOrWhiteSpace(req.Name))
                return BuildResult(400, "角色名称不能为空");

            if (req.Id == 0)
            {
                if (Role.Set.Any(r => r.Name == req.Name))
                    return BuildResult(400, "角色名已存在");

                var item = new Role
                {
                    Name = req.Name.Trim(),
                    Remark = req.Remark,
                };
                item.Save();
            }
            else
            {
                var existing = Role.Get(req.Id);
                if (existing == null)
                    return BuildResult(404, "角色不存在");

                existing.Name = req.Name.Trim();
                existing.Remark = req.Remark;
                existing.Save();
            }
            return BuildResult(0, "保存成功");
        }
    }
}
