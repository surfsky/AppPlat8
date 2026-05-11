using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
    public class ChartEditorModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string ChartJson { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Theme { get; set; } = "dark";

        public void OnGet(string chartJson, string theme)
        {
            ChartJson = chartJson;
            Theme = string.IsNullOrWhiteSpace(theme) ? "dark" : theme.Trim().ToLower();
        }
    }
}
