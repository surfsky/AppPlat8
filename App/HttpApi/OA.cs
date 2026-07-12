using System.Collections.Generic;
using System.Linq;
using App.HttpApi;
using App.DAL.OA;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

namespace App.API
{
    /// <summary>OA 数据接口</summary>
    public class OA
    {
        /// <summary>获取联系人目录树</summary>
        [HttpApi("获取联系人目录树", AuthLogin = true)]
        public static APIResult GetContactMenuTree(long? excludeId = null, long? selectedId = null)
        {
            var all = ContactMenu.Set.AsNoTracking().ToList();
            var allMap = all.ToDictionary(t => t.Id, t => t);
            var visibleMap = all.ToDictionary(t => t.Id, t => t);

            if (excludeId.IsNotEmpty())
            {
                var blockedIds = all.GetDescendants(excludeId).Select(t => t.Id).ToHashSet();
                foreach (var id in blockedIds)
                    visibleMap.Remove(id);
            }

            if (selectedId.IsNotEmpty() && allMap.TryGetValue(selectedId.Value, out var selected))
            {
                var current = selected;
                while (current != null)
                {
                    visibleMap[current.Id] = current;
                    if (!current.ParentId.HasValue)
                        break;
                    if (!allMap.TryGetValue(current.ParentId.Value, out current))
                        break;
                }
            }

            var tree = visibleMap.Values
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList()
                .ToTree();

            return tree.Select(t => t.Export()).ToList().ToResult();
        }

        /// <summary>获取联系人目录平铺列表</summary>
        [HttpApi("获取联系人目录列表", AuthLogin = true)]
        public static APIResult GetContactMenus(string name = null)
        {
            var list = ContactMenu.Search(name)
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList()
                .Select(t => t.Export())
                .ToList();
            return list.ToResult();
        }
    }
}
