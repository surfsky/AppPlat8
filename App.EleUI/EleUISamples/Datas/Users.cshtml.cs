using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;
using System.Linq;

namespace App.Pages.EleUISamples
{
    public class UsersModel : BaseModel
    {
        public User Item { get; set; }
        public List<SelectListItem> RoleList { get; set; }

        public void OnGet()
        {
            RoleList = Data.GetRoles()
                .Select(t => new SelectListItem(t.Name, t.Name))
                .ToList();
        }

        public IActionResult OnGetData(App.Components.Paging pi, string name, string chineseName, string roleName)
        {
            var result = Data.QueryUsers(pi.PageIndex, pi.PageSize, name, chineseName, roleName);
            return BuildResult(0, "success", new
            {
                items = result.Items,
                total = result.Total
            });
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");

            var deletedCount = Data.DeleteUsers(ids);
            return BuildResult(0, $"删除成功，共{deletedCount}条");
        }
    }
}
