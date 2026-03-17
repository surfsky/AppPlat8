using App.DAL;
using App.Utils;
using App.Components;
using App.Web;
using App.HttpApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Entities;

namespace App.Pages.Admins
{
    using Role = App.DAL.Role;

    [CheckPower(Power.RoleView)]
    public class RolesModel : AdminModel
    {
        public List<Role> Items { get; set; }
        public Role Item { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnGetData(Paging pi, string name)
        {
            var q = Role.Search(name);
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0) return BuildResult(400, "参数错误");
            foreach (var id in ids)
            {
                if (id == 1) continue;
                Role.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }
    }
}
