using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    [Auth(Power.GisGeometryView)]
    public class GeometryEditorModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string GeoJson { get; set; }

        public void OnGet(string geojson)
        {
            GeoJson = geojson;
        }
    }
}
