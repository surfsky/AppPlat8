using System;
using System.Collections.Generic;
using App.DAL;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.KB
{
    //[Auth(Power.KbMenuView)]
    public class IndexModel : AdminModel
    {
        public List<KbMenu> MenuTree { get; set; }

        public void OnGet()
        {
            MenuTree = KbMenu.GetTree();
        }
    }
}
