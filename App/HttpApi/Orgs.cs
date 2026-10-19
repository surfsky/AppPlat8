using System.ComponentModel;
using System.Collections.Generic;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using System.Linq;
using System;

namespace App.API
{
    [Scope("Base")]
    [Description("组织")]
    public class Orgs
    {
        [HttpApi("获取所有组织", AuthLogin=true)]
        public static APIResult GetOrgs()
        {
            return App.DAL.Org.Set.OrderBy(o => o.SortId).ToList().ToResult();
        }

        [HttpApi("获取组织树形结构", AuthLogin=true)]
        public static APIResult GetOrgTree()
        {
            return App.DAL.Org.GetTree().ToResult();
        }

        [HttpApi("获取授权组织树", AuthLogin=true, CacheByUser = true, CacheSeconds = 60)]  // 0.273， 0.052， 0.029
        //[HttpApi("获取授权组织树", AuthLogin=true)]  // 0.29， 0.068， 0.06， 0.037， 0.063
        public static APIResult GetAuthOrgTree()
        {
            var user = Auth.GetUser();
            return BuildAuthOrgTree(user).ToResult();
        }

        /// <summary>获取当前用户的授权组织根节点（单值）。admin 返回 null（表示全量根）。</summary>
        public static long? GetAuthOrgRootId(User user)
        {
            if (user == null || Auth.IsAdmin(user))
                return null;
            return user.EffectiveAuthOrgId;
        }


        /// <summary>构建当前用户可见的组织树（授权组织子树可选，补出的祖先仅展示不可选）。</summary>
        public static List<App.DAL.Org> BuildAuthOrgTree(User user)
        {
            if (user == null)
                return new List<App.DAL.Org>();

            // 所有组织数据（基于缓存克隆，避免给共享缓存对象打上 Enabled=false 之类的临时展示标记）
            var all = App.DAL.Org.All
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .Select(t => t.Clone().SetValue(x => x.GeoData, null))
                .ToList();

            // admin
            if (Auth.IsAdmin(user))
                return all.ToTree();

            // root
            var authRootId = GetAuthOrgRootId(user);
            if (!authRootId.HasValue)
                return new List<App.DAL.Org>();

            // 子树节点：当前授权范围内的组织都可选
            var visibleIds = all.GetDescendants(authRootId).Select(t => t.Id).ToHashSet();
            var keepIds = new HashSet<long>(visibleIds);
            var map = all.ToDictionary(t => t.Id, t => t);
            if (map.TryGetValue(authRootId.Value, out var current))
            {
                while (current?.ParentId != null && map.TryGetValue(current.ParentId.Value, out var parent))
                {
                    parent.Enabled = false; // 祖先节点仅用于展示路径，不允许选中
                    if (!keepIds.Add(parent.Id))
                        break;
                    current = parent;
                }
            }

            // filter
            return all.Where(t => keepIds.Contains(t.Id)).ToList().ToTree();
        }


        /// <summary>获取当前用户在指定组织筛选下可见的组织ID集合。</summary>
        public static HashSet<long> GetAuthVisibleOrgIds(User user, long? orgId = null)
        {
            var all = App.DAL.Org.All.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList();
            if (user == null)
                return new HashSet<long>();
            if (Auth.IsAdmin(user))
            {
                return orgId > 0
                    ? all.GetDescendants(orgId).Select(t => t.Id).ToHashSet()
                    : all.Select(t => t.Id).ToHashSet();
            }

            var authRootId = GetAuthOrgRootId(user);
            var visibleIds = authRootId.HasValue
                ? all.GetDescendants(authRootId).Select(t => t.Id).ToHashSet()
                : new HashSet<long>();
            if (orgId > 0)
            {
                if (!visibleIds.Contains(orgId.Value))
                    return new HashSet<long>();
                return all.GetDescendants(orgId).Select(t => t.Id).ToHashSet();
            }
            return visibleIds;
        }
    }
}
