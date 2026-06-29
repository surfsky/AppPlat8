using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;

namespace App.Pages.GIS
{
    public class ApiNavNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public List<ApiNavNode> Children { get; set; } = new();
    }

    [Auth(Power.GisGeometryView)]
    public class ApiManagerModel : AdminModel
    {
        public List<ApiNavNode> NavMenus { get; set; } = new();
        public string DefaultApisUrl { get; set; } = "/GIS/Apis";

        public void OnGet(long? menuId)
        {
            var menus = GisMenu.GetTree().OrderBy(x => x.SortId).ThenBy(x => x.Id).ToList();
            if (menuId.HasValue)
                DefaultApisUrl = $"/GIS/Apis?menuId={menuId.Value}";

            NavMenus = new List<ApiNavNode>
            {
                new ApiNavNode
                {
                    Id = "all",
                    Name = "全部接口",
                    Url = "/GIS/Apis",
                    Children = BuildNavNodes(menus)
                }
            };
        }

        private static List<ApiNavNode> BuildNavNodes(IEnumerable<GisMenu> menus)
        {
            if (menus == null)
                return new List<ApiNavNode>();

            return menus
                .OrderBy(x => x.SortId)
                .ThenBy(x => x.Id)
                .Select(x => new ApiNavNode
                {
                    Id = x.Id.ToString(),
                    Name = x.Name,
                    Url = $"/GIS/Apis?menuId={x.Id}",
                    Children = BuildNavNodes(x.Children)
                })
                .ToList();
        }
    }
}
