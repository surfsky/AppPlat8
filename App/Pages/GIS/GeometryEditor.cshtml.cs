using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using System;

namespace App.Pages.Shared
{
    public class GeometryEditorModel : BaseModel // AuthModel
    {
        [BindProperty(SupportsGet = true)]
        public string GeoJson { get; set; }

        public void OnGet(string dv)
        {
            GeoJson = dv ?? string.Empty;
        }
    }
}
