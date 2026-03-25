using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using App.Web;
using App.Utils;
using Microsoft.AspNetCore.Mvc.Rendering;
using App.Components;
using Microsoft.AspNetCore.Http;
using App.DAL;

namespace App.Pages.EleUISamples
{
    public class ButtonModel : BaseModel
    {
        [BindProperty]
        public bool ShowItem {get;set;} = true;

        public void OnGet(){}

        public IActionResult OnPostSwitch([FromBody] ButtonModel data)
        {
            data.ShowItem = !data.ShowItem;
            return BuildResult(0, "切换成功", data.Export());
        }
    }
}