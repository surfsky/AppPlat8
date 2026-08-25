using System.ComponentModel;
using System.Collections.Generic;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using System.Linq;

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

        /// <summary>构建当前用户可见的组织树。</summary>
        public static List<App.DAL.Org> BuildAuthorizedOrgTree(User user)
        {
            var all = App.DAL.Org.All.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList();
            if (user == null)
                return new List<App.DAL.Org>();
            if (user.Name == "admin")
                return all.ToTree();

            var authRootIds = GetAuthorizedOrgRootIds(user);
            if (authRootIds.Count == 0)
                return new List<App.DAL.Org>();

            var visibleIds = all.GetDescendants(authRootIds).Select(t => t.Id).ToHashSet();
            var keepIds = new HashSet<long>(visibleIds);
            var map = all.ToDictionary(t => t.Id, t => t);
            foreach (var rootId in authRootIds)
            {
                if (!map.TryGetValue(rootId, out var current))
                    continue;
                while (current?.ParentId != null && map.TryGetValue(current.ParentId.Value, out var parent))
                {
                    if (!keepIds.Add(parent.Id))
                        break;
                    current = parent;
                }
            }

            return all.Where(t => keepIds.Contains(t.Id)).ToList().ToTree();
        }

        /// <summary>获取当前用户直接授权的组织根节点。</summary>
        public static List<long> GetAuthorizedOrgRootIds(User user)
        {
            if (user == null)
                return new List<long>();
            if (user.Name == "admin")
                return App.DAL.Org.All.Where(t => t.ParentId == null).Select(t => t.Id).Distinct().ToList();

            return user.GetAuthorizedOrgs()
                .Where(t => t != null && t.Id > 0)
                .Select(t => t.Id)
                .Distinct()
                .ToList();
        }

        /// <summary>获取当前用户在指定组织筛选下可见的组织ID。</summary>
        public static HashSet<long> GetAuthorizedVisibleOrgIds(User user, long? orgId = null)
        {
            var all = App.DAL.Org.All.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList();
            if (user == null)
                return new HashSet<long>();
            if (user.Name == "admin")
                return orgId > 0
                    ? all.GetDescendants(orgId).Select(t => t.Id).ToHashSet()
                    : all.Select(t => t.Id).ToHashSet();

            var authRootIds = GetAuthorizedOrgRootIds(user);
            var visibleIds = all.GetDescendants(authRootIds).Select(t => t.Id).ToHashSet();
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
