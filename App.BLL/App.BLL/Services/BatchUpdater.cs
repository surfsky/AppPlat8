using System;
using System.Collections.Generic;
using System.Linq;
using App.Utils;
using App.Components;
using App.Entities;
using App.DAL;

namespace App.BLL
{
    /// <summary>
    /// 通用批量更新帮助类。给任意 EntityBase<T> 实体批量更新若干字段。
    /// 用法：
    ///   BatchUpdater.Update<CheckObject>(ids, fieldsDict, skipNullOrEmpty:true, Power.CheckObjectEdit, "批量修改企业信息");
    /// fieldsDict 键 = 属性名（大小写不敏感），值 = 目标值。string null/空白 自动跳过（skipNullOrEmpty=true）。
    /// </summary>
    public static class BatchUpdater
    {
        public record UpdateSummary(int Total, int Updated, int Skipped, List<string> Errors);
        public record BatchResult(int Code, string Message, UpdateSummary Data);

        /// <summary>尝试通过上层 App 程序集的 App.Components.Auth.CheckPower(Power) 解析权限（无循环依赖，通过反射解耦）</summary>
        private static bool ResolvePower(Power power)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm.GetName().Name;
                    if (name != "App" && name != "App.Components" && name != "App.Web") continue;
                    var t = asm.GetType("App.Components.Auth", throwOnError: false);
                    if (t == null) continue;
                    var mi = t.GetMethod("CheckPower", new[] { typeof(Power) });
                    if (mi == null) continue;
                    var obj = mi.Invoke(null, new object[] { power });
                    if (obj is bool b) return b;
                }
            }
            catch { /* ignore */ }
            return false;
        }

        /// <summary>批量更新实体字段</summary>
        /// <typeparam name="T">实体类型，继承 EntityBase<T></typeparam>
        /// <param name="ids">目标实体主键列表</param>
        /// <param name="fields">字段名→目标值（object）；目标值类型自动通过 Convertor.ToType 适配</param>
        /// <param name="requirePower">需要的权限，无权限直接返回 403</param>
        /// <param name="logTitle">审计日志前缀（可为空）</param>
        /// <param name="skipNullOrEmpty">空字符串/null 跳过不更新（推荐 true）</param>
        public static BatchResult Update<T>(
            IEnumerable<long> ids,
            IDictionary<string, object> fields,
            Power? requirePower = null,
            string logTitle = null,
            bool skipNullOrEmpty = true,
            Func<Power, bool> checkPower = null)
            where T : EntityBase<T>, new()
        {
            bool PowerPass(Power p) => checkPower?.Invoke(p) ?? ResolvePower(p);
            if (requirePower.HasValue && !PowerPass(requirePower.Value))
                return new BatchResult(403, "无权批量修改", null);

            var idList = (ids ?? Array.Empty<long>()).Where(i => i > 0).Distinct().ToList();
            if (idList.Count == 0)
                return new BatchResult(400, "未选择任何记录", null);

            var effectiveFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (fields != null)
            {
                foreach (var kv in fields)
                {
                    if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                    var val = kv.Value;
                    if (skipNullOrEmpty)
                    {
                        if (val == null) continue;
                        if (val is string s && string.IsNullOrWhiteSpace(s)) continue;
                    }
                    effectiveFields[kv.Key.Trim()] = val;
                }
            }
            if (effectiveFields.Count == 0)
                return new BatchResult(400, "未提供任何要更新的字段（所有字段均为空已跳过）", null);

            // 预先检测 T 类型是否有这些属性（剔除不匹配的列，避免 SetValue 报错）
            var typeProps = typeof(T).GetProperties(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var validFields = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var invalid = new List<string>();
            foreach (var kv in effectiveFields)
            {
                if (!typeProps.TryGetValue(kv.Key, out var pi)) { invalid.Add(kv.Key); continue; }
                if (!pi.CanWrite) { invalid.Add(kv.Key + "(只读)"); continue; }
                validFields[kv.Key] = kv.Value;
            }
            if (validFields.Count == 0)
                return new BatchResult(400,
                    $"无可写属性匹配；已拒绝的字段：{string.Join(", ", invalid)}（{invalid.Count} 个）", null);

            int updated = 0;
            int skipped = 0;
            var errors = new List<string>();
            for (int i = 0; i < idList.Count; i++)
            {
                var id = idList[i];
                T item;
                try { item = EntityBase<T>.Get(id); }
                catch (Exception ex) { errors.Add($"Id={id}: 加载失败:{ex.Message}"); skipped++; continue; }
                if (item == null) { errors.Add($"Id={id}: 记录不存在"); skipped++; continue; }

                bool dirty = false;
                string diff = "";
                foreach (var kv in validFields)
                {
                    var pi = typeProps[kv.Key];
                    object target;
                    try { target = kv.Value.To(pi.PropertyType); }
                    catch (Exception ex) { errors.Add($"Id={id}: 字段{kv.Key}转换失败:{ex.Message}"); continue; }
                    var oldVal = pi.GetValue(item);
                    var eq = (oldVal == null && target == null)
                          || (oldVal != null && oldVal.Equals(target))
                          || (target != null && target.Equals(oldVal));
                    if (eq) continue;
                    try
                    {
                        pi.SetValue(item, target);
                        dirty = true;
                        diff += $" [{kv.Key}: {(oldVal?.ToString() ?? "<null>")} → {(target?.ToString() ?? "<null>")}]";
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Id={id}: 字段{kv.Key}赋值失败:{ex.Message}");
                    }
                }
                if (!dirty) { skipped++; continue; }
                try
                {
                    item.Save(log: false);
                    updated++;
                    if (!string.IsNullOrWhiteSpace(logTitle))
                    {
                        // 审计日志：无 Web 请求上下文时跳过（例如单元测试/后台任务）；有上下文自动走 Auth/Logger 所在上层程序集
                        try
                        {
                            // 反射调用：App.Components.Logger.LogDb(LogLevel.Info, message, from)
                            var loggerAsm = AppDomain.CurrentDomain.GetAssemblies()
                                .FirstOrDefault(a => a.GetName().Name == "App.Components" || a.GetName().Name == "App");
                            if (loggerAsm != null)
                            {
                                var loggerType = loggerAsm.GetType("App.Components.Logger", throwOnError: false);
                                var logLevelType = loggerAsm.GetType("App.Components.LogLevel", throwOnError: false);
                                if (loggerType != null && logLevelType != null)
                                {
                                    var infoLevel = Enum.Parse(logLevelType, "Info");
                                    var mi = loggerType.GetMethod("LogDb", new[] { logLevelType, typeof(string), typeof(string) });
                                    if (mi != null)
                                        mi.Invoke(null, new object[] { infoLevel, $"{logTitle}: Id={id}{diff}", $"BatchUpdater/{typeof(T).Name}" });
                                }
                            }
                        }
                        catch { /* 审计日志失败不影响主流程 */ }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Id={id}: 保存失败:{ex.Message}");
                }
            }
            var summary = new UpdateSummary(idList.Count, updated, skipped, errors);
            if (errors.Count == 0)
                return new BatchResult(0,
                    $"批量修改完成：共 {summary.Total} 条，实际更新 {summary.Updated} 条，跳过 {summary.Skipped} 条",
                    summary);
            return new BatchResult(summary.Updated > 0 ? 1 : 500,
                $"批量修改结束：更新 {summary.Updated} 条，跳过 {summary.Skipped} 条，失败 {errors.Count} 条（详情：{string.Join("；", errors.Take(20))}{(errors.Count > 20 ? "..." : "")}）",
                summary);
        }
    }
}
