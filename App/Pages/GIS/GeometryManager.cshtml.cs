using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Authorization;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
    public class GeometryManagerModel : AdminModel
    {
        public class GeometryNavNode
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Url { get; set; }
            public List<GeometryNavNode> Children { get; set; } = new();
        }

        public List<GeometryNavNode> NavMenus { get; set; } = new();
        public string DefaultGeometriesUrl { get; set; } = "/GIS/Geometries";

        public void OnGet(long? menuId)
        {
            var menus = GisMenu.GetTree().OrderBy(x => x.SortId).ThenBy(x => x.Id).ToList();
            if (menuId.HasValue)
                DefaultGeometriesUrl = $"/GIS/Geometries?menuId={menuId.Value}";

            NavMenus = new List<GeometryNavNode>
            {
                new GeometryNavNode
                {
                    Id = "all",
                    Name = "全部点位",
                    Url = "/GIS/Geometries",
                    Children = BuildNavNodes(menus)
                }
            };
        }

        private static List<GeometryNavNode> BuildNavNodes(IEnumerable<GisMenu> menus)
        {
            if (menus == null)
                return new List<GeometryNavNode>();

            return menus
                .OrderBy(x => x.SortId)
                .ThenBy(x => x.Id)
                .Select(x => new GeometryNavNode
                {
                    Id = x.Id.ToString(),
                    Name = x.Name,
                    Url = $"/GIS/Geometries?menuId={x.Id}",
                    Children = BuildNavNodes(x.Children)
                })
                .ToList();
        }
    }
}
