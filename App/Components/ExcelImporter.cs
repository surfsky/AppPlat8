using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using App.Entities;
using App.Utils;

namespace App.Components
{
    /// <summary>导入选项</summary>
    public class ExcelImportOptions
    {
        /// <summary>业务主键字段（逗号分隔，如 "Name" / "Name,Code"）。命中则更新，未命中则新增。</summary>
        public string KeyField { get; set; }

        /// <summary>宽松模式：单个单元格无法识别时置空并记录警告，该行仍导入</summary>
        public bool Lenient { get; set; }

        /// <summary>是否忽略 Id 列</summary>
        public bool IgnoreId { get; set; }

        /// <summary>当前用户 Id（用于 CreatorId/OwnerId 默认值）</summary>
        public long? UserId { get; set; }

        /// <summary>URL 上的业务参数（如 checkObjectId），作为新增时的默认值</summary>
        public Dictionary<string, string> UrlFieldValues { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>导入结果</summary>
    public class ExcelImportResult
    {
        /// <summary>逐行日志（Excel行号 | 操作 | 状态 | 概述）</summary>
        public List<string> Logs { get; } = new List<string>();

        /// <summary>扫描到的数据行数</summary>
        public int TotalRows { get; set; }
        public int Inserted { get; set; }
        public int Updated { get; set; }
        public int Failed { get; set; }
        /// <summary>解析失败被跳过的行数</summary>
        public int Skipped { get; set; }
        /// <summary>宽松模式下被忽略的单元格数</summary>
        public int IgnoredCells { get; set; }

        public int Success => Inserted + Updated;
        public string Message { get; set; }
    }

    /// <summary>
    /// 通用 Excel 导入器：
    /// 1) 逐行容错解析（一行出错不影响其它行）；
    /// 2) 逐行输出日志（Excel行号 / 插入|更新 / 成功|失败 / 概述）；
    /// 3) 支持 keyField：按业务主键（如 Name）判定已存在则更新（只覆盖 Excel 中出现的列）。
    /// </summary>
    public static class ExcelImporter
    {
        private static readonly string[] SummaryFields =
        {
            "Name", "RealName", "FullName", "Title", "Code", "SocialCreditCode", "Address"
        };

        //----------------------------------------------------------------
        //  属性过滤（模板列与导入列共用同一套规则）
        //----------------------------------------------------------------
        /// <summary>
        /// 获取可导入的属性列表。
        /// 始终排除：NotMapped、非基础类型、只读、索引器、创建人(CreatorId/CreatorName)、责任人(OwnerId/OwnerName)。
        /// 当 ignoreId=true 时，额外排除所有 *Id 属性。
        /// </summary>
        public static List<PropertyInfo> GetImportableProperties(Type entityType, bool ignoreId)
        {
            return entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                .Where(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).IsBasicType())
                .Where(p => !IsAutoManagedProperty(p.Name))
                .Where(p => !ignoreId || !IsIdLikeProperty(p.Name))
                .Where(p =>
                {
                    var ui = p.GetCustomAttribute<UIAttribute>();
                    return ui == null || !ui.ReadOnly;
                })
                .ToList();
        }

        /// <summary>判断属性是否为系统自动维护字段（创建人、责任人等），导入模板不展示</summary>
        private static bool IsAutoManagedProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            return propertyName.Equals("CreatorId", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("CreatorName", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("OwnerId", StringComparison.OrdinalIgnoreCase)
                || propertyName.Equals("OwnerName", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>判断属性是否为ID相关属性</summary>
        private static bool IsIdLikeProperty(string propertyName)
            => !string.IsNullOrWhiteSpace(propertyName)
               && propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// 创建Excel解析器，并把列严格限定为 props 指定的属性集合，
        /// 同时挂上「文本 → 关联表 Id」解析器（XxxId 列可填名称）。
        /// </summary>
        public static object CreateParser(Type entityType, List<PropertyInfo> props)
        {
            var parserType = typeof(ExcelParser<>).MakeGenericType(entityType);
            var allowed = new HashSet<string>(props.Select(p => p.Name), StringComparer.Ordinal);
            Func<PropertyInfo, bool> filter = p => allowed.Contains(p.Name);
            var parser = Activator.CreateInstance(parserType, new object[] { filter });

            try
            {
                parserType.GetProperty(nameof(ExcelParser<object>.ValueResolver))
                          ?.SetValue(parser, new ExcelNameIdResolver());
            }
            catch { }

            return parser;
        }

        //----------------------------------------------------------------
        //  导入
        //----------------------------------------------------------------
        /// <summary>执行导入</summary>
        public static ExcelImportResult Import(Type entityType, Stream stream, string fileName, ExcelImportOptions options = null)
        {
            options ??= new ExcelImportOptions();
            var result = new ExcelImportResult();
            var keyProps = ResolveKeyProperties(entityType, options.KeyField, result);

            var props = GetImportableProperties(entityType, options.IgnoreId);
            var parser = CreateParser(entityType, props);

            // 逐行容错解析
            var fetchMethod = parser.GetType().GetMethod("FetchWithErrors", new[] { typeof(Stream), typeof(string), typeof(bool) });
            var parsed = fetchMethod?.Invoke(parser, new object[] { stream, fileName, options.Lenient }) as ExcelFetchResult;
            if (parsed == null)
                throw new InvalidOperationException("Excel 解析器不可用（FetchWithErrors 未找到）");

            result.TotalRows = parsed.DataRowCount;
            result.Skipped = parsed.SkippedRowCount;
            result.IgnoredCells = parsed.Warnings.Count;

            result.Logs.Add($"共 {parsed.DataRowCount} 行数据，可导入 {parsed.Rows.Count} 行" +
                            (parsed.SkippedRowCount > 0 ? $"，解析失败跳过 {parsed.SkippedRowCount} 行" : ""));

            // 解析警告（宽松模式）
            foreach (var w in parsed.Warnings.OrderBy(t => t.RowNo))
                result.Logs.Add($"第{w.RowNo}行 | 提示 | {w.Column}：{w.Message}");

            // 解析错误（该行未导入）
            foreach (var e in parsed.Errors.OrderBy(t => t.RowNo))
            {
                result.Failed++;
                result.Logs.Add($"第{e.RowNo}行 | 解析失败 | {e.Column}：{e.Message}");
            }

            // 保存方法
            var saveMethod = entityType.GetMethod("Save", new[] { typeof(EntityOp?), typeof(bool) });
            if (saveMethod == null)
                throw new InvalidOperationException("实体未找到可用的 Save(EntityOp?, bool) 方法");

            var matchedProps = parsed.MatchedProperties
                .Select(n => entityType.GetProperty(n, BindingFlags.Public | BindingFlags.Instance))
                .Where(p => p != null && p.CanWrite)
                .ToList();

            // 兼容：keyField 不在本次 Excel 的列里时无法按主键匹配
            var keyUsable = keyProps.Count > 0 && keyProps.All(k => matchedProps.Any(m => SameName(m, k)));
            if (keyProps.Count > 0 && !keyUsable)
                result.Logs.Add($"提示 | 主键字段 {options.KeyField} 不在本次 Excel 的列中，将全部按新增处理");

            // 逐行导入
            foreach (var row in parsed.Rows.OrderBy(r => r.RowNo))
            {
                var item = row.Item;
                var summary = BuildRowSummary(entityType, item);
                var action = "插入";

                try
                {
                    object target = item;
                    var exist = keyUsable ? FindExisting(entityType, keyProps, item) : null;
                    if (exist != null)
                    {
                        action = "更新";
                        target = exist;
                        CopyMatchedValues(matchedProps, item, exist, keyProps);
                    }
                    else
                    {
                        PrepareForInsert(entityType, item, options.UserId);
                        ApplyUrlFieldValues(entityType, item, options.UrlFieldValues);
                    }

                    saveMethod.Invoke(target, new object[] { null, false });

                    if (action == "更新") result.Updated++;
                    else result.Inserted++;

                    result.Logs.Add($"第{row.RowNo}行 | {action} | 成功 | {summary}");
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Logs.Add($"第{row.RowNo}行 | {action} | 失败 | {summary} | {GetInnermostMessage(ex)}");
                }
            }

            result.Message = $"导入完成：新增 {result.Inserted} 条，更新 {result.Updated} 条，失败 {result.Failed} 条";
            if (result.Skipped > 0)
                result.Message += $"，解析跳过 {result.Skipped} 行";
            if (result.IgnoredCells > 0)
                result.Message += $"，忽略 {result.IgnoredCells} 个无法识别的单元格";
            result.Logs.Add(result.Message);

            return result;
        }

        //----------------------------------------------------------------
        //  主键匹配 / 更新
        //----------------------------------------------------------------
        /// <summary>解析 keyField（支持逗号分隔的多字段）</summary>
        private static List<PropertyInfo> ResolveKeyProperties(Type entityType, string keyField, ExcelImportResult result)
        {
            var props = new List<PropertyInfo>();
            if (string.IsNullOrWhiteSpace(keyField))
                return props;

            foreach (var raw in keyField.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;

                var prop = entityType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanRead)
                {
                    result.Logs.Add($"提示 | 主键字段 {name} 不存在于 {entityType.Name}，已忽略");
                    continue;
                }
                if (!props.Any(t => SameName(t, prop)))
                    props.Add(prop);
            }
            return props;
        }

        /// <summary>按主键字段值查找已存在的记录（忽略逻辑删除数据）</summary>
        private static object FindExisting(Type entityType, List<PropertyInfo> keyProps, object source)
        {
            var set = GetEntitySet(entityType);
            if (set == null || keyProps.Count == 0)
                return null;

            var param = Expression.Parameter(entityType, "e");
            Expression body = null;
            foreach (var prop in keyProps)
            {
                var value = prop.GetValue(source);
                if (value == null) return null;                                  // 空值不参与匹配

                Expression left = Expression.Property(param, prop);
                Expression right;
                if (value is string textValue)
                {
                    if (string.IsNullOrWhiteSpace(textValue)) return null;

                    // 去掉两端空格再比较，避免 Excel 里的多余空格导致匹配失败
                    var trimMethod = typeof(string).GetMethod(nameof(string.Trim), Type.EmptyTypes);
                    left = Expression.Call(left, trimMethod);
                    right = Expression.Constant(textValue.Trim(), typeof(string));
                }
                else
                {
                    right = Expression.Constant(value, prop.PropertyType);
                }

                var equal = Expression.Equal(left, right);
                body = body == null ? equal : Expression.AndAlso(body, equal);
            }
            if (body == null) return null;

            var lambda = Expression.Lambda(body, param);
            var whereCall = Expression.Call(typeof(Queryable), "Where", new[] { entityType },
                                            ((IQueryable)set).Expression, Expression.Quote(lambda));
            var query = ((IQueryable)set).Provider.CreateQuery(whereCall);

            foreach (var row in (IEnumerable)query)
                return row;
            return null;
        }

        /// <summary>获取实体的有效数据集（逻辑删除已过滤）</summary>
        private static IQueryable GetEntitySet(Type entityType)
        {
            try
            {
                var genericType = typeof(Entity<>).MakeGenericType(entityType);
                var prop = genericType.GetProperty("ValidSet", BindingFlags.Public | BindingFlags.Static)
                           ?? genericType.GetProperty("Set", BindingFlags.Public | BindingFlags.Static);
                return prop?.GetValue(null) as IQueryable;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>把 Excel 中出现的列的值复制到目标实体上（更新场景；不覆盖 Id 与主键字段）</summary>
        private static void CopyMatchedValues(List<PropertyInfo> matchedProps, object source, object target, List<PropertyInfo> keyProps)
        {
            foreach (var prop in matchedProps)
            {
                if (!prop.CanWrite) continue;
                if (string.Equals(prop.Name, "Id", StringComparison.OrdinalIgnoreCase)) continue;
                if (keyProps.Any(k => SameName(k, prop))) continue;

                prop.SetValue(target, prop.GetValue(source));
            }
        }

        //----------------------------------------------------------------
        //  其它辅助
        //----------------------------------------------------------------
        /// <summary>准备新增（Id 置 0，补 CreatorId/OwnerId 默认值）</summary>
        private static void PrepareForInsert(Type entityType, object item, long? userId)
        {
            var idProp = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (idProp != null && idProp.CanWrite && idProp.PropertyType == typeof(long))
                idProp.SetValue(item, 0L);

            if (userId == null) return;

            foreach (var name in new[] { "CreatorId", "OwnerId" })
            {
                var prop = entityType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null || !prop.CanWrite || prop.PropertyType != typeof(long?)) continue;

                if (prop.GetValue(item) == null)
                    prop.SetValue(item, userId);
            }
        }

        /// <summary>把 URL 参数值作为默认值填入（Excel 已有值时不覆盖）</summary>
        private static void ApplyUrlFieldValues(Type entityType, object item, Dictionary<string, string> urlFieldValues)
        {
            if (urlFieldValues == null || urlFieldValues.Count == 0) return;

            foreach (var kv in urlFieldValues)
            {
                var prop = entityType.GetProperty(kv.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || !prop.CanWrite) continue;

                var existing = prop.GetValue(item);
                if (existing != null && !string.IsNullOrWhiteSpace(existing.ToString())) continue;

                var converted = ConvertValue(kv.Value, prop.PropertyType);
                if (converted != null)
                    prop.SetValue(item, converted);
            }
        }

        /// <summary>字符串转目标类型（支持 Nullable）</summary>
        private static object ConvertValue(string value, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            try
            {
                if (underlying == typeof(string)) return value;
                return Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>行概述：取前几个有值的业务字段拼凑，便于日志辨识</summary>
        private static string BuildRowSummary(Type entityType, object item)
        {
            var parts = new List<string>();
            foreach (var name in SummaryFields)
            {
                var prop = entityType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || prop.PropertyType != typeof(string)) continue;

                var text = (prop.GetValue(item) as string)?.Trim();
                if (string.IsNullOrWhiteSpace(text) || parts.Contains(text)) continue;

                parts.Add(text);
                if (parts.Count >= 3) break;
            }

            if (parts.Count == 0)
            {
                var idProp = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                var id = idProp?.GetValue(item);
                parts.Add(id == null ? "(无概述字段)" : $"(Id={id})");
            }

            var summary = string.Join(" / ", parts);
            return summary.Length > 80 ? summary.Substring(0, 80) + "…" : summary;
        }

        private static bool SameName(PropertyInfo a, PropertyInfo b)
            => string.Equals(a?.Name, b?.Name, StringComparison.OrdinalIgnoreCase);

        private static string GetInnermostMessage(Exception ex)
        {
            var e = ex;
            while (e.InnerException != null) e = e.InnerException;
            return e.Message;
        }
    }
}
