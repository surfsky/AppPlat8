using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using App.DAL;
using App.Utils;

namespace App.Entities
{
    /// <summary>
    /// 当前请求的数据访问作用域。
    /// </summary>
    public class DataAccessScope
    {
        public bool Enabled { get; set; } = true;
        public bool AllowAll { get; set; }
        public bool AllowOrg { get; set; }
        public bool AllowOwn { get; set; }
        public long? UserId { get; set; }
        public long? OrgId { get; set; }
        public bool IncludeSubOrgs { get; set; } = true;
    }

    /// <summary>
    /// 统一数据访问过滤器（按 OrgId/OwnerId 注入）。
    /// </summary>
    public static class DataAccessFilter
    {
        public static IQueryable<T> Apply<T>(IQueryable<T> query, DataAccessScope scope)
            where T : EntityBase, new()
        {
            if (query == null)
                return query;
            if (scope == null || !scope.Enabled || scope.AllowAll)
                return query;

            if (typeof(T) == typeof(User)
                || typeof(T) == typeof(Role)
                || typeof(T) == typeof(RolePower)
                || typeof(T) == typeof(Org))
                return query;

            var hasOrgId = typeof(T).GetProperty("OrgId") != null;
            var hasOwnerId = typeof(T).GetProperty(nameof(EntityBase.OwnerId)) != null;

            // 实体没有组织与责任人字段时，不注入数据过滤。
            if (!hasOrgId && !hasOwnerId)
                return query;

            var useOrg = scope.AllowOrg && hasOrgId;
            var useOwn = scope.AllowOwn && hasOwnerId;

            if (!useOrg && !useOwn)
                return query.Where(t => false);

            var orgIds = useOrg
                ? ResolveOrgIds(scope.OrgId, scope.IncludeSubOrgs)
                : new HashSet<long>();

            return query.Where(t => MatchScope((long?)t.GetValue("OrgId"), t.OwnerId, scope, orgIds));
        }

        public static bool MatchScope(long? entityOrgId, long? entityOwnerId, DataAccessScope scope, ISet<long> orgIds)
        {
            if (scope == null || !scope.Enabled || scope.AllowAll)
                return true;

            bool matchedOrg = false;
            bool matchedOwn = false;

            if (scope.AllowOrg)
            {
                if (entityOrgId.HasValue && orgIds != null)
                    matchedOrg = orgIds.Contains(entityOrgId.Value);
            }

            if (scope.AllowOwn)
            {
                if (scope.UserId.HasValue && entityOwnerId.HasValue)
                    matchedOwn = scope.UserId.Value == entityOwnerId.Value;
            }

            return matchedOrg || matchedOwn;
        }

        /// <summary>解析组织ID列表（包含子组织）</summary>
        public static HashSet<long> ResolveOrgIds(long? rootOrgId, bool includeSubOrgs)
        {
            var ids = new HashSet<long>();
            if (!rootOrgId.HasValue)
                return ids;

            if (!includeSubOrgs)
            {
                ids.Add(rootOrgId.Value);
                return ids;
            }

            var orgIds = Org.All.GetDescendants(rootOrgId).Select(t => t.Id).Distinct().ToList();
            foreach (var id in orgIds)
                ids.Add(id);
            return ids;
        }
    }
}
