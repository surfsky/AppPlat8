using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUISamples
{
    public class RoleUsersUsersModel : BaseModel
    {
        public User Item { get; set; }
        public string RoleName { get; set; }

        public void OnGet(string roleName)
        {
            RoleName = roleName ?? string.Empty;
        }

        public IActionResult OnGetData(App.Components.Paging pi, string roleName, string name, string chineseName)
        {
            var result = Data.QueryUsers(pi.PageIndex, pi.PageSize, name, chineseName, roleName);
            return BuildResult(0, "success", new
            {
                items = result.Items,
                total = result.Total
            });
        }
    }
}
