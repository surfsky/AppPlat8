using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
    public class GeometryManagerModel : AdminModel
    {
        public List<GisMenu> Menus { get; set; } = new();
        public string DefaultGeometriesUrl { get; set; } = "/GIS/Geometries";

        public void OnGet(long? menuId)
        {
            Menus = GisMenu.GetTree().OrderBy(x => x.SortId).ThenBy(x => x.Id).ToList();
            if (menuId.HasValue)
                DefaultGeometriesUrl = $"/GIS/Geometries?menuId={menuId.Value}";
        }
    }
}
