using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.EntityFrameworkCore;

namespace App.API
{
    [Scope("KB")]
    [Description("知识库")]
    public class KB
    {
        /// <summary>获取知识库导航数据。</summary>
        [HttpApi("获取知识库导航数据", AuthLogin = true)]
        public static APIResult GetNavigation(long? orgId = null)
        {
            var user = Auth.GetUser();
            if (user == null)
                return new APIResult(-2, "用户未登录");

            var orgTree = Orgs.BuildAuthorizedOrgTree(user);
            var visibleOrgIds = Orgs.GetAuthorizedVisibleOrgIds(user, orgId);
            if (orgId > 0 && visibleOrgIds.Count == 0)
                return new APIResult(-1, "所选组织不在授权范围内");

            var allMenus = KbMenu.IncludeSet
                .AsNoTracking()
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList();

            var directVisibleMenuIds = BuildDirectVisibleMenuIds(allMenus, visibleOrgIds, orgId > 0);
            var keepMenuIds = BuildKeepMenuIds(allMenus, directVisibleMenuIds);
            var fileLookup = BuildFileLookup(keepMenuIds);

            var roots = allMenus
                .Where(t => keepMenuIds.Contains(t.Id))
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList()
                .ToTree();

            var tree = roots
                .Select(t => ToMenuNode(t, directVisibleMenuIds, keepMenuIds, fileLookup))
                .ToList();

            return new
            {
                orgTree,
                currentOrgId = orgId,
                tree
            }.ToResult();
        }

        /// <summary>筛选当前组织范围内直接可见的目录。</summary>
        static HashSet<long> BuildDirectVisibleMenuIds(List<KbMenu> menus, HashSet<long> visibleOrgIds, bool orgFilterApplied)
        {
            if (orgFilterApplied)
            {
                return menus
                    .Where(t => t != null && t.OrgId != null && visibleOrgIds.Contains(t.OrgId.Value))
                    .Select(t => t.Id)
                    .ToHashSet();
            }
            return menus
                .Where(t => t != null && (t.OrgId == null || visibleOrgIds.Contains(t.OrgId.Value)))
                .Select(t => t.Id)
                .ToHashSet();
        }

        /// <summary>补齐可见目录的父级链路。</summary>
        static HashSet<long> BuildKeepMenuIds(List<KbMenu> menus, HashSet<long> directVisibleMenuIds)
        {
            var keepIds = new HashSet<long>(directVisibleMenuIds);
            var map = menus.ToDictionary(t => t.Id, t => t);
            foreach (var menuId in directVisibleMenuIds)
            {
                if (!map.TryGetValue(menuId, out var current))
                    continue;
                while (current?.ParentId != null && map.TryGetValue(current.ParentId.Value, out var parent))
                {
                    if (!keepIds.Add(parent.Id))
                        break;
                    current = parent;
                }
            }
            return keepIds;
        }

        /// <summary>构建知识库附件索引。</summary>
        static Dictionary<long, List<Att>> BuildFileLookup(HashSet<long> keepMenuIds)
        {
            var lookup = new Dictionary<long, List<Att>>();
            var files = Att.Set
                .AsNoTracking()
                .Where(t => t.Key.StartsWith("KbMenu-"))
                .OrderBy(t => t.SortId)
                .ThenByDescending(t => t.Id)
                .ToList();

            foreach (var file in files)
            {
                var menuId = ParseMenuId(file.Key);
                if (menuId <= 0 || !keepMenuIds.Contains(menuId))
                    continue;
                if (!lookup.TryGetValue(menuId, out var items))
                {
                    items = new List<Att>();
                    lookup[menuId] = items;
                }
                items.Add(file);
            }
            return lookup;
        }

        /// <summary>将目录转换为导航节点。</summary>
        static object ToMenuNode(
            KbMenu menu,
            HashSet<long> directVisibleMenuIds,
            HashSet<long> keepMenuIds,
            Dictionary<long, List<Att>> fileLookup)
        {
            var children = new List<object>();
            foreach (var child in menu.Children ?? new List<KbMenu>())
            {
                if (!keepMenuIds.Contains(child.Id))
                    continue;
                children.Add(ToMenuNode(child, directVisibleMenuIds, keepMenuIds, fileLookup));
            }

            if (directVisibleMenuIds.Contains(menu.Id) && fileLookup.TryGetValue(menu.Id, out var files))
            {
                children.AddRange(files.Select(t => new
                {
                    key = $"file-{t.Id}",
                    id = t.Id,
                    menuId = menu.Id,
                    parentId = menu.Id,
                    name = t.FileName,
                    fileName = t.FileName,
                    fileSize = t.FileSize,
                    fileSizeText = t.FileSizeText,
                    nodeType = "file",
                    orgId = menu.OrgId,
                    orgName = menu.OrgName,
                    url = $"/KB/Manager?menuId={menu.Id}"
                }));
            }

            return new
            {
                key = $"menu-{menu.Id}",
                id = menu.Id,
                menuId = menu.Id,
                parentId = menu.ParentId,
                name = menu.Name,
                fullName = menu.FullName,
                nodeType = "menu",
                orgId = menu.OrgId,
                orgName = menu.OrgName,
                canOpen = directVisibleMenuIds.Contains(menu.Id),
                url = $"/KB/Manager?menuId={menu.Id}",
                children
            };
        }

        /// <summary>从附件键中解析目录ID。</summary>
        static long ParseMenuId(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return 0;
            if (!key.StartsWith("KbMenu-", StringComparison.OrdinalIgnoreCase))
                return 0;
            return long.TryParse(key.Substring("KbMenu-".Length), out var menuId) ? menuId : 0;
        }
    }
}
