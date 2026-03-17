using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.DAL;
using App.HttpApi;
using App.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.Dev
{
    [Auth(Power.Web)]
    public class APIModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
