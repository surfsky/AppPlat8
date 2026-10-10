using App.DAL;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System;
using App.Entities;
using App.Utils;

namespace App.Pages.Shared
{
    [IgnoreAntiforgeryToken]
    public class UserSelectorModel : AuthModel
    {
        public void OnGet() { }

        /// <summary>用户可选范围：admin 全量；其他人只能在自己的授权组织子树中选择。</summary>
        public IActionResult OnGetUsers(string keyword)
        {
            var q = App.DAL.User.Search(keyword: keyword);
            var cu = Auth.GetUser();
            if (!Auth.IsAdmin(cu))
                q = q.FilterByOrg(cu);
            var users = q.SortPageExport();
            return BuildResult(0, "ok", users);
        }
    }
}

