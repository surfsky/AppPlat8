using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace App.Utils
{
    /// <summary>
    /// Excel 操作辅助类
    /// </summary>
    public class ExcelHelper
    {
        // 输出 Excel Xml
        public static string ToExcelXml<T>(IList<T> objs, bool showFieldDescription=false)
        {
            if (objs.IsEmpty())
                return "";

            //var type = typeof(T);
            var type = objs[0].GetType();
            var attrs = new UISetting(type).Items;
            var props = type.GetProperties();

            // 表开始
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            sb.AppendLine(" <Worksheet ss:Name=\"Sheet1\">");
            sb.AppendLine("  <Table>");

            // 默认中文标题（第 1 行）；当 showFieldDescription=true 时追加英文属性名（第 2 行）
            sb.AppendLine("   <Row>");
            foreach (var attr in attrs)
                sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + (attr?.Title ?? "").Replace("<", "＜") + "</Data></Cell>");
            sb.AppendLine("   </Row>");
            if (showFieldDescription)
            {
                sb.AppendLine("   <Row>");
                foreach (var prop in props)
                    sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + prop.Name + "</Data></Cell>");
                sb.AppendLine("   </Row>");
            }


            // 输出数据
            foreach (var obj in objs)
            {
                sb.AppendLine("   <Row>");
                foreach (var prop in props)
                {
                    var val = obj.GetValue(prop.Name).ToText();
                    sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + val.Replace("<", "＜") + "</Data></Cell>");
                }
                sb.AppendLine("   </Row>");
            }

            // 表结束
            sb.AppendLine("  </Table>");
            sb.AppendLine(" </Worksheet>");
            sb.AppendLine("</Workbook>");
            return sb.ToString();
        }

        // 将 DataTable 转化为 ExcelXml
        public static string ToExcelXml(DataTable dt)
        {
            // 表开始
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            sb.AppendLine(" <Worksheet ss:Name=\"Sheet1\">");
            sb.AppendLine("  <Table>");

            // 输出标题和数据
            sb.AppendLine("   <Row>");
            for (int i = 0; i < dt.Columns.Count; i++)
                sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + dt.Columns[i].Caption + "</Data></Cell>");
            sb.AppendLine("   </Row>");

            //输出所有列数据
            foreach (DataRow dr in dt.Rows)
            {
                sb.AppendLine("   <Row>");
                Object[] ary = dr.ItemArray;
                for (int i = 0; i <= ary.GetUpperBound(0); i++)
                    sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + ary[i].ToString().Replace("<", "＜") + "</Data></Cell>");
                sb.AppendLine("   </Row>");
            }

            // 表结束
            sb.AppendLine("  </Table>");
            sb.AppendLine(" </Worksheet>");
            sb.AppendLine("</Workbook>");
            return sb.ToString();
        }

        // 将 IEnumerable<object> + ExportColumnConfig（多层表头）转换为 ExcelXml
        public static string ToExcelXml(IEnumerable<object> objs, ExportColumnConfig cfg, bool showPropertyNameHeader = false)
        {
            cfg = cfg ?? new ExportColumnConfig();
            var leafCols = cfg.GetLeafColumns() ?? new List<ExportColumn>();
            int maxDepth = Math.Max(1, cfg.ComputeHeaderDepth());
            var rows = objs?.ToList() ?? new List<object>();
            var sheetName = cfg.SheetName ?? "Sheet1";
            // sheet 名清洗：非法字符替换；超 31 字符截断（Excel 限制）
            if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);
            foreach (var ch in new[] { '[', ']', ':', '*', '?', '/', '\\' })
                sheetName = sheetName.Replace(ch, '_');
            if (string.IsNullOrWhiteSpace(sheetName)) sheetName = "Sheet1";

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            // 多层表头样式（加粗 + 居中 + 灰底）
            sb.AppendLine(" <Styles>");
            sb.AppendLine("  <Style ss:ID=\"sHeader\">");
            sb.AppendLine("   <Font ss:Bold=\"1\"/>");
            sb.AppendLine("   <Alignment ss:Horizontal=\"Center\" ss:Vertical=\"Center\"/>");
            sb.AppendLine("   <Interior ss:Color=\"#F2F2F2\" ss:Pattern=\"Solid\"/>");
            sb.AppendLine("   <Borders>");
            sb.AppendLine("    <Border ss:Position=\"Bottom\" ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/>");
            sb.AppendLine("    <Border ss:Position=\"Top\"    ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/>");
            sb.AppendLine("    <Border ss:Position=\"Left\"   ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/>");
            sb.AppendLine("    <Border ss:Position=\"Right\"  ss:LineStyle=\"Continuous\" ss:Weight=\"1\"/>");
            sb.AppendLine("   </Borders>");
            sb.AppendLine("  </Style>");
            sb.AppendLine(" </Styles>");

            sb.AppendFormat(" <Worksheet ss:Name=\"{0}\">", XmlEscape(sheetName)).AppendLine();
            sb.AppendLine("  <Table>");

            // 列宽（默认 24；叶子列有 Width 则用 Width/7 换算）
            foreach (var lc in leafCols)
            {
                double w = (lc.Width.HasValue && lc.Width.Value > 0) ? Math.Max(1, lc.Width.Value / 7.0) : 24.0;
                sb.AppendFormat("   <Column ss:Width=\"{0:0.##}\"/>", w).AppendLine();
            }

            // 逐行逐列扫描：维护输出行 grid[rowInDepth][leafIdx] = cellText or null(被 merge 占)
            var headerRows = new List<List<string>>();
            for (int d = 0; d < maxDepth; d++) headerRows.Add(new List<string>());
            int leafCursor = 0;

            void Walk(ExportColumn node, int depth)
            {
                if (node.IsLeaf)
                {
                    int down = (maxDepth - 1) - depth;
                    headerRows[depth].Add(MakeCell(node.Label ?? "", 0, down, true));
                    for (int d = depth + 1; d < maxDepth; d++) headerRows[d].Add(null);
                    leafCursor++;
                    return;
                }
                int start = leafCursor;
                foreach (var c in (node.Children ?? new List<ExportColumn>()))
                    Walk(c, depth + 1);
                int end = leafCursor;
                int across = Math.Max(0, (end - start) - 1);
                // 非叶子：只在当前 depth 占 1 行，子节点在 depth+1 及以下填
                headerRows[depth].Add(MakeCell(node.Label ?? "", across, 0, true));
                // 右侧 across 列都填 null 占位（同一深度的跨列不需要再填 cell，因为 MakeCell 已经 ss:MergeAcross）
                for (int i = 0; i < across; i++) headerRows[depth].Add(null);
            }
            foreach (var root in (cfg.Columns ?? new List<ExportColumn>()))
                Walk(root, 0);

            // 输出多层表头
            for (int d = 0; d < maxDepth; d++)
            {
                sb.AppendLine("   <Row>");
                foreach (var c in headerRows[d])
                {
                    if (c == null) continue;
                    sb.Append("    ").AppendLine(c);
                }
                sb.AppendLine("   </Row>");
            }
            if (showPropertyNameHeader)
            {
                sb.AppendLine("   <Row>");
                foreach (var lc in leafCols)
                    sb.Append("    ").AppendLine(MakeCell(lc.PropertyName ?? "", 0, 0, true));
                sb.AppendLine("   </Row>");
            }

            // 数据行：按叶子列 PropertyName 顺序取值（匿名类或 POCO 都能用反射）
            var typeCache = new Dictionary<Type, Dictionary<string, System.Reflection.PropertyInfo>>();
            foreach (var obj in rows)
            {
                sb.AppendLine("   <Row>");
                foreach (var lc in leafCols)
                {
                    object val = null;
                    if (obj != null && !string.IsNullOrEmpty(lc.PropertyName))
                    {
                        var t = obj.GetType();
                        if (!typeCache.TryGetValue(t, out var map))
                        {
                            map = new Dictionary<string, System.Reflection.PropertyInfo>(StringComparer.Ordinal);
                            foreach (var p in t.GetProperties())
                                map[p.Name] = p;
                            typeCache[t] = map;
                        }
                        if (map.TryGetValue(lc.PropertyName, out var pi))
                            val = pi.GetValue(obj, null);
                    }
                    var s = val == null ? "" : val.ToText();
                    sb.Append("    ").AppendLine(MakeCell(s ?? "", 0, 0, false));
                }
                sb.AppendLine("   </Row>");
            }

            sb.AppendLine("  </Table>");

            // 冻结窗格：冻结顶部表头行 + 左侧 FreezeCols 列
            int freezeTopRows = maxDepth + (showPropertyNameHeader ? 1 : 0);
            if (freezeTopRows > 0 || cfg.FreezeCols > 0)
            {
                sb.AppendLine("  <WorksheetOptions xmlns=\"urn:schemas-microsoft-com:office:excel\">");
                sb.AppendLine("   <FreezePanes/>");
                sb.AppendLine("   <FrozenNoSplit/>");
                // 水平分割（列）：SplitHorizontal = 冻结左侧列数
                if (cfg.FreezeCols > 0)
                    sb.AppendFormat("   <SplitHorizontal>{0}</SplitHorizontal>", cfg.FreezeCols).AppendLine();
                // 垂直分割（行）：SplitVertical = 冻结顶行数
                if (freezeTopRows > 0)
                    sb.AppendFormat("   <SplitVertical>{0}</SplitVertical>", freezeTopRows).AppendLine();
                // Pane 编号：只冻结行=2；只冻结列=1；都冻结=3（右下为活动窗格）
                int paneNo = (cfg.FreezeCols > 0 && freezeTopRows > 0) ? 3
                           : (cfg.FreezeCols > 0 ? 1 : 2);
                sb.AppendFormat("   <ActivePane>{0}</ActivePane>", paneNo).AppendLine();
                sb.AppendLine("  </WorksheetOptions>");
            }

            sb.AppendLine(" </Worksheet>");
            sb.AppendLine("</Workbook>");
            return sb.ToString();
        }

        /// <summary>节点子树最大深度（root=1 起）。</summary>
        static int SubtreeDepth(ExportColumn c)
        {
            if (c == null) return 0;
            if (c.IsLeaf) return 1;
            int max = 0;
            foreach (var child in (c.Children ?? new List<ExportColumn>()))
                max = Math.Max(max, SubtreeDepth(child));
            return 1 + max;
        }

        /// <summary>生成单个 Cell（XML）。</summary>
        static string MakeCell(string text, int mergeAcross, int mergeDown, bool header)
        {
            var attrs = new List<string>();
            if (mergeAcross > 0) attrs.Add($"ss:MergeAcross=\"{mergeAcross}\"");
            if (mergeDown > 0)   attrs.Add($"ss:MergeDown=\"{mergeDown}\"");
            if (header) attrs.Add("ss:StyleID=\"sHeader\"");
            var attrsStr = attrs.Count == 0 ? "" : " " + string.Join(" ", attrs);
            return $"<Cell{attrsStr}><Data ss:Type=\"String\">{XmlEscape(text ?? "")}</Data></Cell>";
        }

        /// <summary>XML 文本转义。</summary>
        static string XmlEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
        }
    }
}