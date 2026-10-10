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

        [HttpApi("获取授权组织树", AuthLogin=true)]
        public static APIResult GetAuthOrgTree()
        {
            var user = Auth.GetUser();
            if (user == null)
                return new APIResult(-2, "用户未登录");
            return BuildAuthorizedOrgTree(user).ToResult();
        }

        /// <summary>构建当前用户可见的组织树（包含当前授权组织子树 + 其所有祖先，以便树展示完整路径）。</summary>
        public static List<App.DAL.Org> BuildAuthorizedOrgTree(User user)
        {
            var all = App.DAL.Org.All.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList();
            if (user == null)
                return new List<App.DAL.Org>();
            if (Auth.IsAdmin(user))
                return all.ToTree();

            var authRootId = GetAuthorizedOrgRootId(user);
            if (!authRootId.HasValue)
                return new List<App.DAL.Org>();

            var visibleIds = all.GetDescendants(authRootId).Select(t => t.Id).ToHashSet();
            var keepIds = new HashSet<long>(visibleIds);
            var map = all.ToDictionary(t => t.Id, t => t);
            if (map.TryGetValue(authRootId.Value, out var current))
            {
                while (current?.ParentId != null && map.TryGetValue(current.ParentId.Value, out var parent))
                {
                    if (!keepIds.Add(parent.Id))
                        break;
                    current = parent;
                }
            }
            return all.Where(t => keepIds.Contains(t.Id)).ToList().ToTree();
        }

        /// <summary>获取当前用户的授权组织根节点（单值）。admin 返回 null（表示全量根）。</summary>
        public static long? GetAuthorizedOrgRootId(User user)
        {
            if (user == null || Auth.IsAdmin(user))
                return null;
            var id = user.EffectiveAuthOrgId;
            return id.HasValue && id.Value > 0 ? id.Value : null;
        }

        /// <summary>兼容方法：获取授权组织根节点列表（单元素列表）。</summary>
        public static List<long> GetAuthorizedOrgRootIds(User user)
        {
            var id = GetAuthorizedOrgRootId(user);
            if (!id.HasValue)
                return Auth.IsAdmin(user)
                    ? App.DAL.Org.All.Where(t => t.ParentId == null).Select(t => t.Id).Distinct().ToList()
                    : new List<long>();
            return new List<long> { id.Value };
        }

        /// <summary>获取当前用户在指定组织筛选下可见的组织ID集合。</summary>
        public static HashSet<long> GetAuthorizedVisibleOrgIds(User user, long? orgId = null)
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

            var authRootId = GetAuthorizedOrgRootId(user);
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

