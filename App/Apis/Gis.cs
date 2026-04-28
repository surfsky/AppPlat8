using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL.GIS;
using App.Entities;
using App.HttpApi;
using App.Utils;

namespace App.API
{
    public class Gis
    {
        [HttpApi("获取几何图形树", AuthLogin = true)]
        public static APIResult GetGeometryTree(long? excludeId = null, long? selectedId = null)
        {
            var all = GisGeometry.IncludeSet.ToList();
            var allMap = all.ToDictionary(t => t.Id, t => t);
            var visibleMap = all.ToDictionary(t => t.Id, t => t);

            if (excludeId.HasValue)
            {
                var blockedIds = all.GetDescendants(excludeId).Select(t => t.Id).ToHashSet();
                foreach (var id in blockedIds)
                {
                    visibleMap.Remove(id);
                }
            }

            if (selectedId.HasValue && allMap.TryGetValue(selectedId.Value, out var selected))
            {
                var current = selected;
                while (current != null)
                {
                    visibleMap[current.Id] = current;
                    if (current.ParentId == null) break;
                    if (!allMap.TryGetValue(current.ParentId.Value, out current)) break;
                }
            }

            var tree = visibleMap.Values.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList().ToTree();
            return tree.ToResult();
        }
    }
}
