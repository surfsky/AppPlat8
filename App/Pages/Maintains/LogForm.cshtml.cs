using System;
using Microsoft.AspNetCore.Mvc;
using App.DAL;
using App.Web;
using App.Components;


namespace App.Pages.Maintains
{
    [CheckPower(Power.MonitorLog)]
    public class LogFormModel : AdminModel
    {
        [BindProperty]
        public Log Item { get; set; }

        public void OnGet(long id)
        {
            Item = Log.Get(id);
        }
    }
}
