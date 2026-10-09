using System;
using Microsoft.AspNetCore.Mvc;
using App.DAL;
using App.Web;
using App.Components;


namespace App.Pages.Maintains
{
    [Auth(Power.MonitorLog)]
    public class LogFormModel : AuthModel
    {
        [BindProperty]
        public Log Item { get; set; }

        public void OnGet(long id)
        {
            Item = Log.Get(id);
        }
    }
}
