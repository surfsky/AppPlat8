using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using App.Utils;
using App.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace App.Entities
{
    /// <summary>
    /// 实体审计辅助器。
    /// 提供：
    /// 1) ShouldAudit：是否对该类型写 Logs 表审计（排除 Log/History/Att/Online 等易产生自激或高频写的类型）
    /// 2) GetTitle：从 Name/Title/Code/FullName/NickName 等属性读"记录名称"，便于审计里"谁删了哪条"一眼看懂
    /// 3) DiffOriginalVsCurrent：EF Entry 保存前 OriginalValues vs 当前 CurrentValues 做字段级 diff，形成"字段名:旧值→新值"串
    /// </summary>
    public static class EntityAuditHelper
    {
        // 写审计时忽略的实体类型（避免自激写循环、避免高频无意义日志）
        private static readonly HashSet<string> _noAudit = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Log", "History", "Att", "Online",
            "CheckObjectTag", "CheckSheetTag", "CheckTaskTag",  // 多对多关系表，无意义
            "UserOrg", "RolePower", "RoleMenu",                // 用户/角色 关联表，变更通常由聚合根保存触发，实体内部不会单独 Save
        };

        /// <summary>是否对该类型做 Logs 表审计（写数据库）</summary>
        public static bool ShouldAudit(Type t)
        {
            if (t == null) return false;
            var name = t.Name;
            if (_noAudit.Contains(name)) return false;
            // 动态代理类型去掉 "Castle.Proxies.xxxProxy"
            if (name.EndsWith("Proxy", StringComparison.OrdinalIgnoreCase))
            {
                var baseName = t.BaseType?.Name;
                if (!string.IsNullOrEmpty(baseName) && _noAudit.Contains(baseName)) return false;
            }
            return true;
        }

        private static readonly string[] _titleProps = new[]
        {
            "Name", "Title", "Code", "FullName", "NickName", "UserName", "Mobile", "Remark"
        };

        /// <summary>读取实体的"名称/标题"，用于审计日志"名称=xxx"字段；无则返回 - </summary>
        public static string GetTitle(object entity)
        {
            if (entity == null) return "-";
            Type t = entity.GetType();
            foreach (var prop in _titleProps)
            {
                var p = t.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (p == null || !p.PropertyType.IsBasicType()) continue;
                try
                {
                    var v = p.GetValue(entity, null);
                    if (v != null && !string.IsNullOrWhiteSpace(v.ToString()))
                        return v.ToString();
                }
                catch { }
            }
            return "-";
        }

        /// <summary>
        /// 取 EF EntityEntry 的字段差异（OriginalValues vs CurrentValues）。
        /// 返回格式："字段1:旧值→新值; 字段2:旧→新"，跳过未变更的列。
        /// 无法比较或没有变化返回 null/空字符串。
        /// </summary>
        public static string DiffOriginalVsCurrent(EntityEntry entry)
        {
            if (entry == null) return null;
            var parts = new List<string>();
            try
            {
                foreach (var prop in entry.CurrentValues.Properties)
                {
                    if (!IsTrackedProperty(prop)) continue;
                    var propName = prop.Name;
                    object orig;
                    object cur;
                    try
                    {
                        orig = entry.OriginalValues[prop];
                        cur = entry.CurrentValues[prop];
                    }
                    catch
                    {
                        continue;
                    }
                    bool eq = (orig == null && cur == null) || (orig != null && orig.Equals(cur));
                    if (eq) continue;

                    parts.Add($"{propName}: {ToString(orig)} → {ToString(cur)}");
                }
            }
            catch
            {
                return null;
            }
            return parts.Count == 0 ? "" : string.Join("; ", parts);
        }

        // 是否纳入 diff：忽略 NotMapped / 纯导航（非 Property）/ 自增的 Id / CreateDt / UpdateDt 这类常见无关字段
        private static bool IsTrackedProperty(Microsoft.EntityFrameworkCore.Metadata.IProperty p)
        {
            if (p == null) return false;
            var n = p.Name;
            // 忽略的字段列表（这些是自动的，diff 没有审计意义）
            var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CreateDt", "UpdateDt", "CreatorId", "OwnerId", "Version"
            };
            if (skip.Contains(n)) return false;
            // 只对基本类型比较
            var t = p.ClrType;
            if (t == null) return false;
            return t.IsBasicType();
        }

        private static string ToString(object o)
        {
            if (o == null) return "<null>";
            try
            {
                var s = o.ToString();
                if (string.IsNullOrEmpty(s)) return "<empty>";
                if (s.Length > 128) s = s.Substring(0, 128) + "...";
                return s;
            }
            catch
            {
                return "<error>";
            }
        }
    }
}
