using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;

namespace App.Pages.Admins
{
    [Auth(Power.RoleEdit)]
    public class RolePowerModel : AdminModel
    {
        public long RoleId { get; set; }

        public void OnGet(long roleId)
        {
            RoleId = roleId;
        }

        public IActionResult OnGetGroupPowers()
        {
            var groupNames = typeof(Power).GetEnumGroups();
            var infos = typeof(Power).GetEnumInfos();
            var result = new List<object>();

            foreach (var groupName in groupNames)
            {
                var items = infos.Where(t => t.Group == groupName).Select(t => new
                {
                    id = t.Id,
                    title = t.Title
                }).ToList();

                result.Add(new
                {
                    groupName,
                    powers = items
                });
            }

            return BuildResult(0, "success", result);
        }

        public IActionResult OnGetRolePowerIds(long roleId)
        {
            var ids = RolePower.Set.Where(t => t.RoleId == roleId).Select(t => (long)t.PowerId).ToList();
            return BuildResult(0, "success", ids);
        }

        public IActionResult OnPostSaveRolePowers([FromBody] SaveRolePowerRequest req)
        {
            if (req == null || req.RoleId <= 0)
                return BuildResult(400, "参数错误");

            RolePower.SetRolePowers(req.RoleId, req.PowerIds ?? new List<long>());
            return BuildResult(0, "保存成功");
        }

        public class SaveRolePowerRequest
        {
            public long RoleId { get; set; }
            public List<long> PowerIds { get; set; }
        }
    }
}
