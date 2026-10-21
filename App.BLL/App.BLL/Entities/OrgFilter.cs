using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using App.DAL;

namespace App.Entities
{
    /// <summary>
    /// 授权组织相关扩展方法。
    /// 约定：用户的实际生效授权组织为 EffectiveAuthOrgId（= AuthOrgId ?? OrgId）。
    /// 数据过滤思路：
    ///   - 取目标 rootId 下的所有组织 Id（包含自身）；
    ///   - 对含 OrgId 属性的实体，按 OrgId IN (子树集合) 做收敛；
    ///   - 表达式树保证可被 EF Core 翻译为 SQL。
    /// 注意：本类不引用 App 层的 Auth/HttpContext 组件；调用方需自行取出当前用户 EffectiveAuthOrgId 传入。
    /// </summary>
    public static class OrgFilter
    {
        //-------------------------------------------
        // 1. 组织 Id 子树集合
        //-------------------------------------------
        /// <summary>获取授权组织 Id 集合（包含目标 Id）</summary>
        public static HashSet<long> GetChildOrgIds(long? rootId)
        {
            var allOrgs = DAL.Org.All;
            if (!rootId.HasValue || rootId.Value <= 0 || allOrgs == null || allOrgs.Count == 0)
                return new HashSet<long>();
            var des = allOrgs.GetDescendants(rootId);
            return des?.Select(t => t.Id).Where(t => t > 0).ToHashSet() ?? new HashSet<long>();  // 为什么要用hashtable？ 用排序后的列表不行吗？
        }


        /// <summary>获取组织自身及其所有上级组织 Id（含自身）。用于"发布范围含下属"的反向过滤。</summary>
        public static HashSet<long> GetAncestorOrgIds(long? orgId)
        {
            var result = new HashSet<long>();
            var allOrgs = DAL.Org.All;
            if (!orgId.HasValue || orgId.Value <= 0 || allOrgs == null || allOrgs.Count == 0)
                return result;

            var map = allOrgs.ToDictionary(t => t.Id, t => t);
            var current = orgId;
            while (current.HasValue && map.TryGetValue(current.Value, out var org))
            {
                if (!result.Add(org.Id)) break;      // 防环
                current = org.ParentId;
            }
            return result;
        }


        //-------------------------------------------
        // 2. 单值越权校验（管理员 vs 目标）
        //-------------------------------------------
        /// <summary>判断用户是否可访问目标组织</summary>
        public static bool IsAuth(User user,long? orgId)
        {
            if (user?.Name == "admin") return true;
            return IsAuth(user.AuthOrgId, orgId);
        }

        /// <summary>判断 orgId 是否落在 rootOrgId 的可见子树中。</summary>
        static bool IsAuth(long? rootOrgId, long? orgId)
        {
            // 两者都空：相等则算在范围内
            if (!rootOrgId.HasValue || rootOrgId.Value <= 0)
                return !orgId.HasValue || orgId.Value <= 0;

            if (!orgId.HasValue || orgId.Value <= 0)
                return false;

            var orgIds = GetChildOrgIds(rootOrgId);
            return orgIds.Contains(orgId.Value);
        }

        //-------------------------------------------
        // 3. IQueryable 表达式树过滤（保证 SQL 翻译）
        //-------------------------------------------
        /// <summary>给 IQueryable 查询添加组织过滤条件：where orgId in (user.AuthOrgId...) 用户授权组织树（含自身）。</summary>
        public static IQueryable<T> FilterByOrg<T>(
            this IQueryable<T> query,
            User user,
            string orgPropName = "OrgId",
            int maxOrNodes = 512)
        {
            if (query == null) return query;
            if (user != null && user.Name == "admin") return query; // 管理员：不过滤
            if (!user?.AuthOrgId.HasValue ?? true) return query; // 未登录/批处理或无授权组织 → 不过滤
            var prop = ResolveOrgProperty<T>(orgPropName);
            if (prop == null) return query; // 目标实体没有 OrgId → 原样返回

            // 附加 in 或 or 语法
            var ids = GetChildOrgIds(user.AuthOrgId);
            if (ids.Count > maxOrNodes)
                return query.Where(BuildInPredicate<T>(prop, ids));
            else
                return query.Where(BuildOrPredicate<T>(prop, ids));
        }


        //-------------------------------------------
        // 4. 反向过滤：发布范围含下属
        //-------------------------------------------
        /// <summary>
        /// 反向组织过滤：保留"发布组织为当前用户组织或其上级组织"的数据（OrgId 为空视为公共数据，人人可见）。
        /// 用于"发布范围含下属"的语义：上级组织发布的数据，其下属组织均能看到（如公告）。
        /// 即 SQL： OrgId IS NULL OR OrgId IN (用户组织及其祖先组织)。
        /// </summary>
        public static IQueryable<T> FilterByAncestOrg<T>(
            this IQueryable<T> query,
            User user,
            string orgPropName = "OrgId")
        {
            if (query == null) return query;
            if (user != null && user.Name == "admin") return query;   // 管理员：不过滤
            var prop = ResolveOrgProperty<T>(orgPropName);
            if (prop == null) return query;                           // 目标实体没有 OrgId 属性 → 原样返回

            var ids = GetAncestorOrgIds(user?.AuthOrgId);
            return query.Where(BuildOrPredicate<T>(prop, ids, includeNull: true));
        }


        //-------------------------------------------
        // 内部工具
        //-------------------------------------------
        /// <summary>获取实体的 long/long? 组织属性；不存在或类型不符时返回 null。</summary>
        private static PropertyInfo ResolveOrgProperty<T>(string orgPropName)
        {
            var prop = typeof(T).GetProperty(orgPropName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (prop == null) return null;
            var propType = prop.PropertyType;
            return (propType == typeof(long) || propType == typeof(long?)) ? prop : null;
        }


        /// <summary>将 long? 表达式转为 long 表达式（必要时取 Value）。</summary>
        private static Expression MakeNonNullable(Expression expr)
        {
            if (expr.Type == typeof(long)) return expr;
            if (expr.Type == typeof(long?))
                return Expression.Property(expr, nameof(Nullable<long>.Value));
            return Expression.Convert(expr, typeof(long));
        }

        /// <summary>构造 "prop in (...)" 的 Lambda。</summary>
        private static Expression<Func<T, bool>> BuildInPredicate<T>(PropertyInfo prop, HashSet<long> ids)
        {
            var param = Expression.Parameter(typeof(T), "t");
            var orgPropExpr = Expression.Property(param, prop);
            var listExpr = Expression.Constant(ids.ToList(), typeof(List<long>));
            var containsCall = Expression.Call(
                typeof(Enumerable),
                nameof(Enumerable.Contains),
                new[] { typeof(long) },
                listExpr,
                MakeNonNullable(orgPropExpr)
            );
            var lambda = Expression.Lambda<Func<T, bool>>(containsCall, param);
            return lambda;
        }

        /// <summary>
        /// 构造 "prop == id1 || prop == id2 || ..." 的 Lambda，支持 long / long? 属性。
        /// - includeNull = true 时额外匹配 prop 为空的情形（视为公共数据，人人可见）；
        /// - 若 ids 为空：includeNull 时等价于只匹配空值，否则恒为 false。
        /// </summary>
        private static Expression<Func<T, bool>> BuildOrPredicate<T>(PropertyInfo prop, IEnumerable<long> ids, bool includeNull = false)
        {
            var param = Expression.Parameter(typeof(T), "t");
            Expression left = Expression.Property(param, prop);
            var isNullable = prop.PropertyType == typeof(long?);

            // 空值分支：可空属性 + 需包含公共数据才有
            Expression body = (includeNull && isNullable)
                ? Expression.Equal(left, Expression.Constant(null, typeof(long?)))
                : null;

            foreach (var id in ids)
            {
                var right = isNullable
                    ? Expression.Constant(id, typeof(long?))
                    : Expression.Constant(id, typeof(long));
                var eq = Expression.Equal(left, right);
                body = body == null ? eq : Expression.OrElse(body, eq);
            }

            return Expression.Lambda<Func<T, bool>>(body ?? Expression.Constant(false), param);
        }
    }
}
