using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Pages.EleUISamples
{
    public class RoleUsersModel : BaseModel
    {
        public List<RoleUsersNavItem> Roles { get; set; } = new();
        public string DefaultUsersUrl { get; set; }
        public string AllUsersUrl => BuildUsersUrl(string.Empty);
        public int AllUsersCount { get; set; }

        public void OnGet()
        {
            Roles = Data.GetRoles()
                .Select(r => new RoleUsersNavItem
                {
                    Name = r.Name,
                    UserCount = Data.GetUsers(r.Name).Count,
                    UsersUrl = BuildUsersUrl(r.Name)
                })
                .ToList();

            AllUsersCount = Data.GetUsers().Count;

            var firstRole = Roles.FirstOrDefault()?.Name ?? string.Empty;
            DefaultUsersUrl = BuildUsersUrl(firstRole);
        }

        private static string BuildUsersUrl(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
                return "/EleUISamples/Datas/RoleUsersUsers";

            return $"/EleUISamples/Datas/RoleUsersUsers?roleName={Uri.EscapeDataString(roleName)}";
        }
    }

    public class RoleUsersNavItem
    {
        public string Name { get; set; }
        public int UserCount { get; set; }
        public string UsersUrl { get; set; }
    }
}
