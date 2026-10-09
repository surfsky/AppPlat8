using App.DAL;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.Pages.Shared
{
    [IgnoreAntiforgeryToken]
    public class UserSelectorModel : AuthModel
    {
        public void OnGet() { }

        public IActionResult OnGetUsers(string keyword)
        {
            var users = App.DAL.User.Search(keyword: keyword).SortPageExport();
            return BuildResult(0, "ok", users);
        }
    }
}
