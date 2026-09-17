using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Utils
{
    /// <summary>Excel 导出列（叶子或分组节点）。</summary>
    public class ExportColumn
    {
        /// <summary>列显示标题（合并单元格里显示的中文）。</summary>
        public string Label { get; set; }

        /// <summary>绑定属性名（叶子节点必填）。对应 T 的 PropertyInfo.Name 或匿名类属性名。</summary>
        public string PropertyName { get; set; }

        /// <summary>列宽（可空，单位字符）。</summary>
        public double? Width { get; set; }

        /// <summary>对齐方式（Left/Center/Right）。</summary>
        public string Align { get; set; }

        /// <summary>子列（非空=分组节点；叶子节点为 null/空）。</summary>
        public List<ExportColumn> Children { get; set; } = new List<ExportColumn>();

        /// <summary>是否叶子列（无子列=true）。</summary>
        public bool IsLeaf => Children == null || Children.Count == 0;
    }

    /// <summary>Excel 导出列配置（多层表头）。</summary>
    public class ExportColumnConfig
    {
        /// <summary>Sheet 名。</summary>
        public string SheetName { get; set; } = "Sheet1";

        /// <summary>最外层列树（根）。</summary>
        public List<ExportColumn> Columns { get; set; } = new List<ExportColumn>();

        /// <summary>冻结列数（0 = 不冻结）。</summary>
        public int FreezeCols { get; set; } = 0;

        /// <summary>
        /// 计算整个列配置的表头最大深度（决定需要多少行来写多层表头）。
        /// 叶子：深度=1；非叶子：深度= max(子列深度)+1。
        /// </summary>
        public int ComputeHeaderDepth()
        {
            int Depth(ExportColumn c) => c.IsLeaf ? 1 : 1 + (c.Children?.Select(Depth).DefaultIfEmpty(0).Max() ?? 0);
            return Columns?.Select(Depth).DefaultIfEmpty(1).Max() ?? 1;
        }

        /// <summary>按左→右顺序返回所有叶子列的 PropertyName（保证按表头布局顺序）。</summary>
        public List<string> GetLeafPropertyNames()
        {
            var list = new List<string>();
            void Walk(ExportColumn c)
            {
                if (c.IsLeaf)
                {
                    list.Add(c.PropertyName);
                    return;
                }
                foreach (var child in c.Children ?? new List<ExportColumn>())
                    Walk(child);
            }
            foreach (var c in Columns ?? new List<ExportColumn>())
                Walk(c);
            return list;
        }

        /// <summary>返回所有叶子节点（按左→右顺序）。</summary>
        public List<ExportColumn> GetLeafColumns()
        {
            var list = new List<ExportColumn>();
            void Walk(ExportColumn c)
            {
                if (c.IsLeaf) { list.Add(c); return; }
                foreach (var child in c.Children ?? new List<ExportColumn>())
                    Walk(child);
            }
            foreach (var c in Columns ?? new List<ExportColumn>())
                Walk(c);
            return list;
        }
    }
}
