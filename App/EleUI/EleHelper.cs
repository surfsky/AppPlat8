using System;
using System.Collections.Generic;
using System.Linq;
using App.Entities;
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
        /// <summary>将枚举转换为选项列表</summary>
        public static List<ListItem> ToListItems(Type enumType)
        {
            return EnumHelper.GetEnumInfos(enumType)
                             .Select(e => new ListItem(e.Value, e.Title))
                             .ToList();
        }
        
        /// <summary>构建树结构</summary>
        /// <param name="all"></param>
        /// <param name="parentId">Parent item id</param>
        public static List<TreeItem> ToTreeItems<T>(List<T> all, long? parentId) where T : ITree<T>, ISort
        {
            var nodes = new List<TreeItem>();
            var list = all.Where(c => c.ParentId == parentId).OrderBy(m => m.SortId).ToList();
            foreach (var item in list)
            {
                var children = ToTreeItems(all, item.Id);
                var node = new TreeItem
                {
                    Value = item.Id,
                    Label = item.Name,
                    Children = children.Any() ? children : null
                };
                nodes.Add(node);
            }
            return nodes;
        }
    }
}