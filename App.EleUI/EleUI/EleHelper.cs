using System;
using System.Collections.Generic;
using System.Linq;
using App.Utils;

namespace App.EleUI
{
    /// <summary>
    /// Select item 节点。
    /// 同 Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
    /// </summary>
    public class ListItem
    {
        public object Value { get; set; }
        public string Label { get; set; }
        public ListItem() { }
        public ListItem(object value, string label)
        {
            this.Value = value;
            this.Label = label;
        }
    }


    /// <summary>
    /// Tree item
    /// https://element-plus.org/en-US/component/tree-select#treeselect
    /// </summary>
    public class TreeItem
    {
        public object Value { get; set; }
        public string Label { get; set; }
        public List<TreeItem> Children { get; set; }

        public TreeItem(){}
        public TreeItem(object value, string label, List<TreeItem> children = null)
        {
            this.Value = value;
            this.Label = label;
            this.Children = children;
        }
    }


    /// <summary>
    /// Element plus ui 帮助类
    /// </summary>
    public static class EleHelper
    {
        /// <summary>字符串序列转为选项列表（Value=Label）</summary>
        public static List<ListItem> ToOptions(IEnumerable<string> items)
        {
            if (items == null) return new List<ListItem>();
            return items
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new ListItem(x, x))
                .ToList();
        }

        /// <summary>对象序列按选择器转为选项列表</summary>
        public static List<ListItem> ToOptions<T>(
            IEnumerable<T> items,
            Func<T, object> valueSelector,
            Func<T, string> labelSelector)
        {
            if (items == null) return new List<ListItem>();
            if (valueSelector == null) throw new ArgumentNullException(nameof(valueSelector));
            if (labelSelector == null) throw new ArgumentNullException(nameof(labelSelector));

            return items
                .Select(x => new ListItem(valueSelector(x), labelSelector(x)))
                .ToList();
        }

        /// <summary>对象序列按同一字段转为选项列表（Value=Label）</summary>
        public static List<ListItem> ToOptions<T>(
            IEnumerable<T> items,
            Func<T, string> textSelector)
        {
            if (items == null) return new List<ListItem>();
            if (textSelector == null) throw new ArgumentNullException(nameof(textSelector));

            return items
                .Select(x => textSelector(x))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => new ListItem(x, x))
                .ToList();
        }

        /// <summary>将枚举转换为选项列表</summary>
        public static List<ListItem> ToListItems(Type enumType)
        {
            return EnumHelper.GetEnumInfos(enumType)
                             .Select(e => new ListItem(e.Value, e.Title))
                             .ToList();
        }
        
        /// <summary>构建树结构（默认读取 Id/ParentId/Name/SortId 字段）。</summary>
        /// <param name="all">源集合</param>
        /// <param name="parentId">父级 Id</param>
        public static List<TreeItem> ToTreeItems<T>(List<T> all, long? parentId)
        {
            var nodes = new List<TreeItem>();
            if (all == null || all.Count == 0)
                return nodes;

            var list = all
                .Where(c => GetLong(c, "ParentId") == parentId)
                .OrderBy(m => GetLong(m, "SortId") ?? 0)
                .ToList();

            foreach (var item in list)
            {
                var id = GetLong(item, "Id");
                if (!id.HasValue)
                    continue;

                var children = ToTreeItems(all, id.Value);
                var node = new TreeItem
                {
                    Value = id.Value,
                    Label = GetString(item, "Name"),
                    Children = children.Any() ? children : null
                };
                nodes.Add(node);
            }
            return nodes;
        }

        private static long? GetLong(object obj, string propertyName)
        {
            var value = GetPropertyValue(obj, propertyName);
            if (value == null)
                return null;
            return Convert.ToInt64(value);
        }

        private static string GetString(object obj, string propertyName)
        {
            var value = GetPropertyValue(obj, propertyName);
            return value?.ToString();
        }

        private static object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            var prop = obj.GetType().GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            return prop?.GetValue(obj);
        }
    }
}