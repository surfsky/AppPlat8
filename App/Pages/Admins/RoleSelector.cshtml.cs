using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Admins
{
    [Auth(Power.RolePowerEdit)]
    public class RoleSelectorModel : AdminModel
    {
        public List<Role> Roles { get; set; } = new List<Role>();
        public long? RoleId { get; set; }

        public void OnGet(long? roleId)
        {
            Roles = Role.Set.OrderBy(t => t.Id).ToList();
            RoleId = roleId ?? Roles.FirstOrDefault()?.Id;
        }

        public IActionResult OnGetRoles()
        {
            var roles = Role.Set.OrderBy(t => t.Id).Select(t => new { id = t.Id, name = t.Name }).ToList();
            return BuildResult(0, "success", roles);
        }
    }
}
