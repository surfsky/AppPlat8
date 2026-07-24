using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;

namespace App.Pages.GIS
{
    // 导航节点类
    public class GeometryNavNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public long? MenuId { get; set; }
        public string Url { get; set; }
        public string ListUrl { get; set; }
        public string MapUrl { get; set; }
        public List<GeometryNavNode> Children { get; set; } = new();
    }

    [Auth(Power.GisGeometryView)]
    public class GeometryManagerModel : AdminModel
    {
        public List<GeometryNavNode> NavMenus { get; set; } = new();
        public string CurrentMode { get; set; } = "list";
        public string DefaultFrameUrl { get; set; } = "/GIS/Geometries";

        /// <summary>加载页面</summary>
        public void OnGet(long? menuId, string mode)
        {
            CurrentMode = mode == "map" ? "map" : "list";
            var userId = GetUserId();
            var editableIds = userId.HasValue
                ? RoleGisMenu.GetUserEditableMenuIds(userId.Value).ToHashSet()
                : new HashSet<long>();

            var menus = FilterGeometryMenus(GisMenu.GetTree().OrderBy(x => x.SortId).ThenBy(x => x.Id).ToList(), editableIds);
            DefaultFrameUrl = BuildUrl(menuId, CurrentMode == "map");

            NavMenus = new List<GeometryNavNode>
            {
                new GeometryNavNode
                {
                    Id = "all",
                    Name = "全部图层",
                    Url = BuildUrl(null, CurrentMode == "map"),
                    ListUrl = BuildUrl(null, false),
                    MapUrl = BuildUrl(null, true),
                    Children = BuildNavNodes(menus)
                }
            };
        }

        /// <summary>构建导航节点</summary>
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
                    MenuId = x.Id,
                    Url = BuildUrl(x.Id, false),
                    ListUrl = BuildUrl(x.Id, false),
                    MapUrl = BuildUrl(x.Id, true),
                    Children = BuildNavNodes(x.Children)
                })
                .ToList();
        }

        /// <summary>仅保留数据来源为 Geometry 的菜单树</summary>
        private static List<GisMenu> FilterGeometryMenus(IEnumerable<GisMenu> menus, HashSet<long> editableIds)
        {
            if (menus == null)
                return new List<GisMenu>();

            var list = new List<GisMenu>();
            foreach (var item in menus)
            {
                if (item == null)
                    continue;

                var isGeometry = (item.DataFrom ?? GisDataFrom.Geometry) == GisDataFrom.Geometry;
                if (!isGeometry)
                    continue;

                var children = FilterGeometryMenus(item.Children ?? new List<GisMenu>(), editableIds);
                var allowed = editableIds != null && editableIds.Contains(item.Id);
                if (!allowed && children.Count == 0)
                    continue;

                var clone = item.Clone();
                clone.Children = children;
                list.Add(clone);
            }

            return list;
        }

        /// <summary>构建目标地址</summary>
        private static string BuildUrl(long? menuId, bool isMap)
        {
            var baseUrl = isMap ? "/GIS/GeometryMap" : "/GIS/Geometries";
            return menuId.HasValue ? $"{baseUrl}?menuId={menuId.Value}" : baseUrl;
        }
    }
}
