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

            if (excludeId != null && excludeId > 0)
            {
                var blockedIds = all.GetDescendants(excludeId.Value).Select(t => t.Id).ToHashSet();
                foreach (var id in blockedIds)
                    visibleMap.Remove(id);
            }

            if (selectedId != null && selectedId > 0 && allMap.TryGetValue(selectedId.Value, out var selected))
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

        /// <summary>获取当前用户可见的通讯录目录树（节点带 canEdit 标记）</summary>
        [HttpApi("获取当前用户可见的通讯录目录树", AuthLogin = true)]
        public static APIResult GetUserVisibleContactMenuTree(long userId)
        {
            var seeIds = new HashSet<long>(RoleContactMenu.GetUserVisibleMenuIds(userId));
            var editIds = new HashSet<long>(RoleContactMenu.GetUserEditableMenuIds(userId));
            return BuildContactMenuTree(seeIds, editIds).ToResult();
        }

        /// <summary>根据 seeIds / editIds 构建目录树（同时携带父链以保证目录完整）</summary>
        static List<object> BuildContactMenuTree(HashSet<long> seeIds, HashSet<long> editIds)
        {
            if (seeIds.Count == 0)
                return new List<object>();

            // 包含所有需要展示的节点：可见节点 + 可见节点的祖先（保证树结构完整）
            var all = ContactMenu.Set.AsNoTracking().ToList();
            var allMap = all.ToDictionary(t => t.Id, t => t);

            var keep = new HashSet<long>(seeIds);
            foreach (var id in seeIds)
            {
                if (!allMap.TryGetValue(id, out var node)) continue;
                var current = node;
                while (current != null && current.ParentId.HasValue && allMap.TryGetValue(current.ParentId.Value, out var parent))
                {
                    if (!keep.Add(parent.Id)) break;
                    current = parent;
                }
            }

            var roots = all
                .Where(t => keep.Contains(t.Id))
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList()
                .ToTree();

            return roots.Select(t => ToContactNode(t, keep, editIds)).ToList();
        }

        static object ToContactNode(ContactMenu menu, HashSet<long> keep, HashSet<long> editIds)
        {
            return new
            {
                id = menu.Id,
                parentId = menu.ParentId,
                name = menu.Name,
                fullName = menu.FullName,
                sortId = menu.SortId,
                treeLevel = menu.TreeLevel,
                canEdit = editIds.Contains(menu.Id),
                children = (menu.Children ?? new List<ContactMenu>())
                    .Where(c => keep.Contains(c.Id))
                    .Select(c => ToContactNode(c, keep, editIds))
                    .ToList()
            };
        }
    }
}
