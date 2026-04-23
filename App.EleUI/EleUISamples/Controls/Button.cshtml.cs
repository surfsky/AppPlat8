using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using App.Utils;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;

namespace App.Pages.EleUISamples
{
    public class ButtonModel : BaseModel
    {
        [BindProperty]
        public bool ShowItem {get;set;} = true;

        public class SwitchRequest
        {
            public bool ShowItem { get; set; }
        }

        public void OnGet(){}

        public IActionResult OnPostSwitch([FromBody] SwitchRequest data)
        {
            var next = !(data?.ShowItem ?? false);
            return BuildResult(0, "切换成功", new { showItem = next });
        }
    }
}