using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using App.DAL;
using App.Utils;
using Microsoft.EntityFrameworkCore;

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
        // 系统级实体白名单（不参与数据权限过滤）
        private static readonly HashSet<Type> _systemTypes = new HashSet<Type>
        {
            typeof(User), typeof(Role), typeof(RolePower), typeof(RoleMenu), typeof(UserOrg),
            typeof(Org), typeof(Menu), typeof(Online), typeof(Log), typeof(SiteConfig),
            typeof(AIConfig), typeof(Sequence), typeof(VerifyCode), typeof(IPFilter),
            typeof(Message), typeof(Att), typeof(Application), typeof(OpenApp), typeof(Site),
            typeof(AliDingConfig), typeof(AliSmsConfig),
            // KB 知识库：目录属于全局内容，模块内部用 OrgId 自行控制可见性
            typeof(KbMenu),
            // CMS 文档库：目录和内容按模块内 OrgId 自行控制
            typeof(Article), typeof(ArticleMenu),
        };

        public static IQueryable<T> Apply<T>(IQueryable<T> query, DataAccessScope scope)
            where T : EntityBase, new()
        {
            if (query == null)
                return query;
            if (scope == null || !scope.Enabled || scope.AllowAll)
                return query;

            if (_systemTypes.Contains(typeof(T)))
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

            return query.Where(BuildScopePredicate<T>(useOrg, useOwn, scope, orgIds));
        }

        /// <summary>构造可被 EF 翻译的强类型表达式树（避免反射 GetValue 无法翻译）</summary>
        private static Expression<Func<T, bool>> BuildScopePredicate<T>(
            bool useOrg, bool useOwn, DataAccessScope scope, HashSet<long> orgIds)
            where T : EntityBase, new()
        {
            var param = Expression.Parameter(typeof(T), "t");
            Expression body = null;

            if (useOrg)
            {
                var orgIdProp = typeof(T).GetProperty("OrgId");
                Expression orgExpr;
                if (orgIdProp != null)
                {
                    orgExpr = Expression.Property(param, orgIdProp);
                }
                else
                {
                    var efProp = typeof(EF).GetMethod(nameof(EF.Property), BindingFlags.Public | BindingFlags.Static)
                        ?.MakeGenericMethod(typeof(long?));
                    orgExpr = Expression.Call(null, efProp, param, Expression.Constant("OrgId"));
                }
                // orgIds.Contains(entityOrgId.Value)  (当 entityOrgId.HasValue)
                var hasValue = Expression.Property(orgExpr, nameof(Nullable<long>.HasValue));
                var valueExpr = Expression.Property(orgExpr, nameof(Nullable<long>.Value));
                var contains = Expression.Call(
                    Expression.Constant(orgIds ?? new HashSet<long>()),
                    typeof(HashSet<long>).GetMethod(nameof(HashSet<long>.Contains), new[] { typeof(long) }),
                    valueExpr);
                var orgMatch = Expression.AndAlso(hasValue, contains);
                body = orgMatch;
            }

            if (useOwn)
            {
                var ownerExpr = Expression.Property(param, nameof(EntityBase.OwnerId));
                Expression<Func<long?, long?, bool>> ownCompare =
                    (owner, uid) => owner.HasValue && uid.HasValue && owner.Value == uid.Value;
                var ownMatch = Expression.Invoke(ownCompare, ownerExpr, Expression.Constant(scope.UserId, typeof(long?)));
                body = (body == null) ? ownMatch : Expression.OrElse(body, ownMatch);
            }

            return Expression.Lambda<Func<T, bool>>(body ?? Expression.Constant(false), param);
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
