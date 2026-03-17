using App.DAL;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;

namespace App.Pages.Shared
{
    [IgnoreAntiforgeryToken]
    public class UserSelectorModel : AdminModel
    {
        public void OnGet()
        {
        }

        public IActionResult OnGetUsers(string keyword)
        {
            var query = App.DAL.User.Set.Where(u => u.InUsed == true);
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(u => u.Name.Contains(keyword) || u.RealName.Contains(keyword));
            }

            var users = query.OrderBy(u => u.RealName ?? u.Name).Take(20).Select(u => new 
            { 
                id = u.Id, 
                name = u.RealName ?? u.Name, 
                username = u.Name, 
                dept = u.Org != null ? u.Org.Name : "" 
            }).ToList();

            return BuildResult(0, "ok", users);
        }
    }
}
