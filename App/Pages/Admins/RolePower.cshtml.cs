using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Z.EntityFramework.Plus;

namespace App.Pages.Admin
{
    [CheckPower(Power.RolePowerEdit)]
    public class RolePowerModel : AdminModel
    {
        public void OnGet()
        {
        }

        /// <summary>获取角色列表</summary>
        public IActionResult OnGetRoles()
        {
            var roles = Role.Set.OrderBy(t => t.Id).Select(t => new { Id = t.Id, t.Name }).ToList();
            return new JsonResult(new { code = 0, data = roles });
        }

        /// <summary>获取所有权限分组及权限</summary>
        public IActionResult OnGetGroupPowers()
        {
            var groupNames = typeof(Power).GetEnumGroups();
            groupNames.Sort(); // 默认排序

            var infos = typeof(Power).GetEnumInfos();
            var result = new List<object>();

            foreach (var groupName in groupNames)
            {
                var items = infos.Where(t => t.Group == groupName).Select(t => new 
                { 
                    id = t.Id, 
                    name = t.Title, // Element Plus 习惯用 label/name
                    title = t.Title 
                }).ToList();

                result.Add(new
                {
                    groupName = groupName,
                    powers = items
                });
            }

            return new JsonResult(new { code = 0, data = result });
        }

        /// <summary>获取指定角色的权限Id列表</summary>
        public IActionResult OnGetRolePowerIds(long roleId)
        {
            var ids = RolePower.Set.Where(t => t.RoleId == roleId).Select(t => (long)t.PowerId).ToList();
            return new JsonResult(new { code = 0, data = ids });
        }

        /// <summary>保存角色权限</summary>
        public IActionResult OnPostSaveRolePowers([FromBody] SaveRolePowerRequest req)
        {
            if (!CheckPower(Power.RolePowerEdit))
            {
                return new JsonResult(new { code = 403, msg = "无权操作" });
            }

            if (req == null || req.RoleId <= 0)
            {
                return new JsonResult(new { code = 400, msg = "参数错误" });
            }

            // 更新当前角色新的权限列表
            // 注意：SetRolePowers 内部是先删后加
            RolePower.SetRolePowers(req.RoleId, req.PowerIds ?? new List<long>());

            return new JsonResult(new { code = 0, msg = "保存成功" });
        }

        public class SaveRolePowerRequest
        {
            public long RoleId { get; set; }
            public List<long> PowerIds { get; set; }
        }
    }
}
