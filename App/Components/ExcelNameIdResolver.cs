using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using App.Entities;
using App.Utils;

namespace App.Components
{
    /// <summary>
    /// 导入用「文本 → 关联表 Id」解析器。
    /// 适用场景：Excel 里的 XxxId / XxxIds 列填的不是数字，而是名称/全称/编码等文本，
    /// 例如 OrgId 列填「龙港市应急管理局」、CheckerId 列填「张三」、RoleIds 列填「管理员,操作员」。
    /// 解析顺序：
    ///   1. 精确匹配（Name / FullName / Code / ChineseName / Title / UserName ...）
    ///   2. 树形全称的末段（如 FullName「浙江省/温州市/龙港市/龙港市应急管理局」→「龙港市应急管理局」）
    ///   3. 去掉末尾括号后缀（如「XX厂（个体工商户）」→「XX厂」）
    /// 匹配到多条或匹配不到时返回失败，并把原因带给 ExcelParser 做人话提示。
    /// </summary>
    public class ExcelNameIdResolver : IExcelValueResolver
    {
        /// <summary>参与名称匹配的字符串属性（按优先级；覆盖 组织/用户/角色/标签 等常见命名字段）</summary>
        private static readonly string[] MatchProperties =
        {
            "FullName", "Name", "RealName", "DisplayName", "ChineseName", "NickName",
            "Title", "Code", "No", "Number", "UserName", "Account", "Mobile", "Phone", "Tel"
        };

        /// <summary>不应参与名称匹配的属性（避免把路径/密码等当成名称索引）</summary>
        private static readonly HashSet<string> ExcludedMatchProperties = new(StringComparer.OrdinalIgnoreCase)
        {
            "Password", "Pwd", "PasswordHash", "FileName", "FilePath", "FilePathName",
            "Url", "Uri", "GeoJson", "Remark", "Description", "Content", "Address"
        };

        private static readonly char[] MultiValueSeparators = { ',', '，', ';', '；', '、', '|' };

        private readonly Func<Type, IEnumerable<object>> _rowLoader;
        private readonly Dictionary<Type, EntityIndex> _indexCache = new();
        private readonly Dictionary<Type, string[]> _matchPropsCache = new();

        /// <summary>某实体的「名称 → Id」索引 + 有效 Id 集合</summary>
        private sealed class EntityIndex
        {
            public Dictionary<string, List<long>> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<long> Ids { get; } = new();
        }

        public ExcelNameIdResolver() : this(null) { }

        /// <param name="rowLoader">自定义行加载器（默认取实体的缓存全表 Entity&lt;T&gt;.All，便于单元测试注入）</param>
        public ExcelNameIdResolver(Func<Type, IEnumerable<object>> rowLoader)
        {
            _rowLoader = rowLoader ?? LoadRowsFromOrm;
        }

        public bool TryResolve(PropertyInfo property, string rawText, out object value, out string error)
        {
            value = null;
            error = null;
            if (property == null)
                return false;

            var isList = property.Name.EndsWith("Ids", StringComparison.OrdinalIgnoreCase);
            if (!isList && !property.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
                return false;   // 只处理 XxxId / XxxIds

            var text = (rawText ?? string.Empty).Trim();
            if (text.Length == 0)
                return false;   // 空值按原逻辑处理（null / 默认值）

            var entityType = ResolveRelatedType(property.DeclaringType, property.Name, isList);
            if (entityType == null)
                return false;   // 找不到关联实体，交回默认转换（报原错误）

            var index = GetIndex(entityType);

            // 多值：RoleIds = "管理员,操作员"
            if (isList)
            {
                var parts = text.Split(MultiValueSeparators, StringSplitOptions.RemoveEmptyEntries)
                                .Select(t => t.Trim())
                                .Where(t => t.Length > 0)
                                .ToList();
                if (parts.Count == 0)
                    return false;

                var ids = new List<long>();
                foreach (var part in parts)
                {
                    if (!TryResolveOne(part, index, entityType, out var id, out error))
                        return false;
                    if (!ids.Contains(id))
                        ids.Add(id);
                }
                value = ids;
                return true;
            }

            // 单值：OrgId = "龙港市应急管理局"
            if (!TryResolveOne(text, index, entityType, out var singleId, out error))
                return false;

            value = singleId;
            return true;
        }

        //----------------------------------------------------------------
        //  内部实现
        //----------------------------------------------------------------
        /// <summary>解析地址文本对应的 Id</summary>
        private static bool TryResolveOne(string text, EntityIndex index, Type entityType, out long id, out string error)
        {
            id = 0;
            error = null;

            // 纯数字且确实是有效 Id 时直接用；否则继续按名称/编码匹配（如手机号作为账号）
            if (long.TryParse(text, out var numeric) && numeric > 0 && index.Ids.Contains(numeric))
            {
                id = numeric;
                return true;
            }

            foreach (var key in BuildLookupKeys(text))
            {
                if (!index.Names.TryGetValue(key, out var ids) || ids.Count == 0)
                    continue;

                if (ids.Count > 1)
                {
                    error = $"「{text}」在 {entityType.GetTitle()} 中匹配到 {ids.Count} 条记录（Id: {string.Join(",", ids.Take(10))}），请改填 Id";
                    return false;
                }

                id = ids[0];
                return true;
            }

            error = $"在 {entityType.GetTitle()} 中找不到「{text}」（可填 {DescribeMatchFields(entityType)}），或直接填 Id";
            return false;
        }

        /// <summary>描述可用的匹配字段（用于错误提示）</summary>
        private static string DescribeMatchFields(Type entityType)
        {
            var titles = new List<string>();
            foreach (var name in MatchProperties)
            {
                var prop = entityType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || prop.PropertyType != typeof(string)) continue;

                var ui = prop.GetCustomAttribute<UIAttribute>();
                var title = string.IsNullOrWhiteSpace(ui?.Title) ? prop.Name : ui.Title;
                if (!titles.Contains(title)) titles.Add(title);
                if (titles.Count >= 4) break;
            }
            return titles.Count == 0 ? "名称" : string.Join("/", titles);
        }

        /// <summary>文本的候选匹配键（原文 / 全称末段 / 去括号后缀）</summary>
        private static IEnumerable<string> BuildLookupKeys(string text)
        {
            var normalized = (text ?? string.Empty).Trim();
            if (normalized.Length == 0)
                yield break;

            yield return normalized;

            // 全称「A/B/C」→ 末段 C
            var slash = normalized.LastIndexOfAny(new[] { '/', '\\' });
            if (slash >= 0 && slash < normalized.Length - 1)
                yield return normalized.Substring(slash + 1).Trim();

            // 去掉末尾括号：「XX厂（个体工商户）」→「XX厂」
            var stripped = StripTrailingBracket(normalized);
            if (!string.Equals(stripped, normalized, StringComparison.Ordinal))
                yield return stripped;

            if (slash >= 0 && slash < normalized.Length - 1)
            {
                var tail = StripTrailingBracket(normalized.Substring(slash + 1).Trim());
                if (!string.Equals(tail, normalized, StringComparison.Ordinal))
                    yield return tail;
            }
        }

        /// <summary>去掉文本末尾的括号后缀，如「（个体工商户）」「(无证）」</summary>
        private static string StripTrailingBracket(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var t = text.TrimEnd();
            if (t.Length < 2)
                return t;

            var end = t.Length - 1;
            var endChar = t[end];
            if (endChar != '）' && endChar != ')')
                return t;

            var openChar = endChar == '）' ? '（' : '(';
            var start = t.LastIndexOf(openChar);
            // 只处理“紧贴结尾”的括号，且括号内不是空
            if (start <= 0 || end - start < 2)
                return t;

            return t.Substring(0, start).TrimEnd();
        }

        /// <summary>获取某实体的 名称/编码 → Id 索引（按类型缓存；已逻辑删除的记录不参与匹配）</summary>
        private EntityIndex GetIndex(Type entityType)
        {
            if (_indexCache.TryGetValue(entityType, out var cached))
                return cached;

            var index = new EntityIndex();
            try
            {
                var rows = _rowLoader(entityType) ?? Enumerable.Empty<object>();
                var idProp = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                var isDelProp = entityType.GetProperty("IsDel", BindingFlags.Public | BindingFlags.Instance);
                var matchProps = GetMatchProperties(entityType);

                foreach (var row in rows)
                {
                    if (row == null) continue;

                    var idValue = idProp?.GetValue(row);
                    if (idValue == null) continue;
                    var id = Convert.ToInt64(idValue);
                    if (id <= 0) continue;

                    // 逻辑删除的数据不参与「名称 → Id」匹配（否则会和有效数据重名）
                    if (isDelProp != null && isDelProp.GetValue(row) is bool isDeleted && isDeleted)
                        continue;

                    index.Ids.Add(id);
                    foreach (var key in BuildRowKeys(entityType, row, matchProps))
                        AddIndex(index.Names, key, id);
                }
            }
            catch (Exception ex)
            {
                UtilConfig.Log("Import", $"加载关联表 {entityType.Name} 失败: {ex.Message}");
            }

            _indexCache[entityType] = index;
            return index;
        }

        /// <summary>取某实体的名称匹配字段（固定列表 + 其它 *Name/*Code 字符串字段兜底）</summary>
        private string[] GetMatchProperties(Type entityType)
        {
            if (_matchPropsCache.TryGetValue(entityType, out var cached))
                return cached;

            var names = new List<string>();
            void Add(string name)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                if (ExcludedMatchProperties.Contains(name)) return;
                if (names.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
                names.Add(name);
            }

            foreach (var name in MatchProperties)
                Add(name);

            // 兜底：实体上其它 *Name / *Code 字符串字段（如 RealFieldName），让未预置的实体也能按名称导入
            foreach (var prop in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != typeof(string) || !prop.CanRead) continue;
                if (prop.GetIndexParameters().Length > 0) continue;
                if (prop.Name.EndsWith("Name", StringComparison.OrdinalIgnoreCase)
                    || prop.Name.EndsWith("Code", StringComparison.OrdinalIgnoreCase))
                    Add(prop.Name);
            }

            var result = names.ToArray();
            _matchPropsCache[entityType] = result;
            return result;
        }

        /// <summary>从一行数据里取出所有可匹配的文本键（名称/全称/编码等）</summary>
        private static IEnumerable<string> BuildRowKeys(Type entityType, object row, string[] matchProps)
        {
            foreach (var name in matchProps ?? MatchProperties)
            {
                var prop = entityType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || prop.PropertyType != typeof(string))
                    continue;

                var text = (prop.GetValue(row) as string)?.Trim();
                if (string.IsNullOrEmpty(text))
                    continue;

                foreach (var key in BuildLookupKeys(text))
                    yield return key;
            }
        }

        private static void AddIndex(Dictionary<string, List<long>> index, string key, long id)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            if (!index.TryGetValue(key, out var list))
            {
                list = new List<long>();
                index[key] = list;
            }
            if (!list.Contains(id))
                list.Add(id);
        }

        /// <summary>默认行加载器：使用实体自身的缓存全表（Entity&lt;T&gt;.All）</summary>
        private static IEnumerable<object> LoadRowsFromOrm(Type entityType)
        {
            IEnumerable rows = null;
            try
            {
                var genericType = typeof(Entity<>).MakeGenericType(entityType);
                var allProp = genericType.GetProperty("All", BindingFlags.Public | BindingFlags.Static);
                rows = allProp?.GetValue(null) as IEnumerable;
            }
            catch (Exception ex)
            {
                UtilConfig.Log("Import", $"读取 {entityType.Name} 缓存全表失败: {ex.Message}");
                return Enumerable.Empty<object>();
            }

            if (rows == null)
                return Enumerable.Empty<object>();

            return rows.Cast<object>();
        }

        /// <summary>
        /// 推断 XxxId / XxxIds 对应的关联实体类型：
        /// 1) 同名导航属性（OrgId → Org、CheckerId → Checker）；
        /// 2) ParentId → 自身类型（树形自引用）；
        /// 3) 按类名在 App.DAL 命名空间查找（UserId → App.DAL.User）。
        /// </summary>
        private static Type ResolveRelatedType(Type ownerType, string propertyName, bool isList)
        {
            if (ownerType == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            var suffixLength = isList ? "Ids".Length : "Id".Length;
            if (propertyName.Length <= suffixLength)
                return null;

            var baseName = propertyName.Substring(0, propertyName.Length - suffixLength);

            // 1) 导航属性（优先，最准确）
            var nav = ownerType.GetProperty(baseName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (nav != null && typeof(Entity).IsAssignableFrom(nav.PropertyType))
                return nav.PropertyType;

            // 2) 树形自引用
            if (string.Equals(baseName, "Parent", StringComparison.OrdinalIgnoreCase))
                return ownerType;

            // 3) 按类名查找 App.DAL 下的实体
            foreach (var fullName in new[] { $"App.DAL.{baseName}", $"App.DAL.GIS.{baseName}" })
            {
                var type = Reflector.GetType(fullName);
                if (type != null && typeof(Entity).IsAssignableFrom(type))
                    return type;
            }

            // 4) 兜底：已加载程序集中查找
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                var found = types.FirstOrDefault(t =>
                    t.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase)
                    && (t.Namespace ?? string.Empty).StartsWith("App.DAL", StringComparison.Ordinal)
                    && typeof(Entity).IsAssignableFrom(t));
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
