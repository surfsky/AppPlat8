using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

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
        public static HashSet<long> GetAuthOrgIds(long? rootId)  => GetAuthOrgIds(App.DAL.Org.All, rootId);

        /// <summary>获取授权组织 Id 集合（包含目标 Id）</summary>
        private static HashSet<long> GetAuthOrgIds(List<App.DAL.Org> allOrgs, long? rootId)
        {
            if (!rootId.HasValue || rootId.Value <= 0 || allOrgs == null || allOrgs.Count == 0)
                return new HashSet<long>();
            var des = allOrgs.GetDescendants(rootId);
            return des?.Select(t => t.Id).Where(t => t > 0).ToHashSet() ?? new HashSet<long>();
        }


        //-------------------------------------------
        // 2. 单值越权校验（管理员 vs 目标）
        //-------------------------------------------
        /// <summary>
        /// 判断 targetOrgId 是否落在 adminAuthOrgId 的可见子树中。
        /// 规则：
        ///   - adminAuthOrgId 为空视为未授权（admin 未配置任何组织）：只有 targetOrgId 为空才放行；
        ///   - targetOrgId 为空：在 adminOrg 也为空时放行，否则视为"越权尝试写入空值"拒绝；
        ///   - 其它：targetOrgId 必须是 adminAuthOrgId 子树中的节点（包含自身）。
        /// </summary>
        public static bool IsAuth(long? authOrgId, long? targetOrgId)
        {
            // 两者都空：相等则算在范围内
            if (!authOrgId.HasValue || authOrgId.Value <= 0)
                return !targetOrgId.HasValue || targetOrgId.Value <= 0;

            if (!targetOrgId.HasValue || targetOrgId.Value <= 0)
                return false;

            var orgIds = GetAuthOrgIds(authOrgId);
            return orgIds.Contains(targetOrgId.Value);
        }

        //-------------------------------------------
        // 3. IQueryable 表达式树过滤（保证 SQL 翻译）
        //-------------------------------------------
        /// <summary>
        /// 按授权组织子树过滤：OrgId ∈ adminAuthOrgId 子树（含自身）。
        /// - 若实体无 OrgId 属性：原样返回；
        /// - 若 authOrgId 为空：原样返回（视为无授权上下文，调用方自行处理）；
        /// - 其他情况：构造 SQL 可翻译的 OR 树表达式（避免从本地集合 Contains 造成客户端评估）。
        ///   当子树节点数超过 maxOrNodes 时退化为 Contains(本地集合)，由 EF Core 翻译为参数化 IN。
        /// </summary>
        /// <typeparam name="T">实体类型</typeparam>
        /// <param name="query">原查询</param>
        /// <param name="authOrgId">管理员授权组织（EffectiveAuthOrgId）</param>
        /// <param name="orgPropName">组织外键属性名，默认 "OrgId"</param>
        /// <param name="maxOrNodes">超过阈值后退化为 Contains；默认 512</param>
        public static IQueryable<T> FilterByOrg<T>(
            this IQueryable<T> query,
            long? authOrgId,
            string orgPropName = "OrgId",
            int maxOrNodes = 512)
        {
            if (query == null) return query;

            // 无授权上下文 → 不过滤（调用方为批处理/未登录）
            if (!authOrgId.HasValue || authOrgId.Value <= 0)
                return query;

            var type = typeof(T);
            var prop = type.GetProperty(orgPropName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (prop == null) return query; // 目标实体没有 OrgId → 原样返回

            var propType = prop.PropertyType;
            if (propType != typeof(long) && propType != typeof(long?))
                return query;

            // 取子树集合
            var ids = GetAuthOrgIds(authOrgId);
            if (ids.Count == 0)
            {
                // 找不到子树但管理员有 AuthOrgId → 说明数据异常，保守返回空（避免越权）
                return query.Where(t => false);
            }

            // 超过阈值 → 使用 EF Core 8 支持的 Contains 参数化
            if (ids.Count > maxOrNodes)
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
                return query.Where(lambda);
            }

            // 否则展开为 OR 树（long? == value || long? == value ...）
            // 对应 SQL：OrgId = 1 OR OrgId = 2 OR ...。SQLite/SqlServer 均支持。
            return query.Where(BuildOrEqualPredicate<T>(prop, ids));
        }

        /// <summary>便捷重载：从当前用户对象中取 EffectiveAuthOrgId 再过滤。</summary>
        public static IQueryable<T> FilterByOrg<T>(
            this IQueryable<T> query,
            App.DAL.User currentUser,
            string orgPropName = "OrgId",
            int maxOrNodes = 512)
        {
            var authOrgId = currentUser?.EffectiveAuthOrgId;
            return query.FilterByOrg(authOrgId, orgPropName, maxOrNodes);
        }

        //-------------------------------------------
        // 内部工具
        //-------------------------------------------
        /// <summary>将 long? 表达式转为 long 表达式（必要时取 Value）。</summary>
        private static Expression MakeNonNullable(Expression expr)
        {
            if (expr.Type == typeof(long)) return expr;
            if (expr.Type == typeof(long?))
                return Expression.Property(expr, nameof(Nullable<long>.Value));
            return Expression.Convert(expr, typeof(long));
        }

        /// <summary>
        /// 构造 "prop == ids[0] || prop == ids[1] || ..." 的 Lambda。
        /// 支持 long / long? 属性：若属性可空则用 GetValueOrDefault() 与常量 0 比较，
        /// 否则直接比较；为兼容 EF Core 翻译，使用统一的 (Nullable<long>).GetValueOrDefault() 方案。
        /// </summary>
        private static Expression<Func<T, bool>> BuildOrEqualPredicate<T>(PropertyInfo prop, IEnumerable<long> ids)
        {
            var param = Expression.Parameter(typeof(T), "t");
            Expression left = Expression.Property(param, prop);
            // 归一为 long 值
            Expression normalizedLeft;
            if (prop.PropertyType == typeof(long?))
                normalizedLeft = Expression.Call(left, nameof(Nullable<long>.GetValueOrDefault), Type.EmptyTypes);
            else
                normalizedLeft = left;

            Expression body = null;
            foreach (var id in ids)
            {
                var c = Expression.Constant(id, typeof(long));
                var eq = Expression.Equal(normalizedLeft, c);
                body = body == null ? eq : Expression.OrElse(body, eq);
            }
            if (body == null) body = Expression.Constant(false);
            return Expression.Lambda<Func<T, bool>>(body, param);
        }
    }
}
