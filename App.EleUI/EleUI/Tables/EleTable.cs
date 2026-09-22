using App.Components;
using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using App.Utils; 
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text;

namespace App.EleUI
{
    /// <summary>
    /// 表格上下文（包含工具栏、列定义等）
    /// </summary>
    public class TableContext
    {
        public StringBuilder ToolbarHtml { get; set; } = new StringBuilder();
        public StringBuilder ColumnsHtml { get; set; } = new StringBuilder();
    }

    //-----------------------------------------------------------------
    // Table
    //-----------------------------------------------------------------
    /// <summary>
    /// 表格标签助手。包含工具栏、列定义、分页、排序、弹窗等功能
    /// </summary>
    [HtmlTargetElement("EleTable")]
    [RestrictChildren("Toolbar", "Columns")]
    public class EleTable : EleControl
    {
        private static readonly object _idLock = new();
        private static int _idCounter = 0;

        [HtmlAttributeName("Title")]
        public string Title { get; set; } = "列表";

        [HtmlAttributeName("FormPage")]
        public string FormPage { get; set; }

        [HtmlAttributeName("FormDrawerSize")]
        public string FormDrawerSize { get; set; }

        [HtmlAttributeName("DataHandler")]
        public string DataHandler { get; set; } = "?handler=Data";

        [HtmlAttributeName("DeleteHandler")]
        public string DeleteHandler { get; set; } = "?handler=Delete";

        [HtmlAttributeName("EnableBatch")]
        public bool EnableBatch { get; set; } = false;

        [HtmlAttributeName("RowKey")]
        public string RowKey { get; set; }

        [HtmlAttributeName("BuildMode")]
        public EleAppBuildMode BuildMode { get; set; } = EleAppBuildMode.Client;

        [HtmlAttributeName("PageSize")]
        public int? PageSize { get; set; } = 20;

        [HtmlAttributeName("ShowPage")]
        public bool ShowPage { get; set; } = true;

        [HtmlAttributeName("SortField")]
        public string SortField { get; set; } = "";

        [HtmlAttributeName("SortDirection")]
        public string SortDirection { get; set; } = "ASC";

        /// <summary>全局表头对齐（left/center/right，可被单个列的 HeaderAlign/LabelAlign 覆盖。别名：TitleAlign。默认 center。</summary>
        [HtmlAttributeName("HeaderAlign")]
        public string HeaderAlign { get; set; } = "center";

        [HtmlAttributeName("TitleAlign")]
        public string TitleAlign { get; set; }

        /// <summary>表头长文字是否自动换行。true 时表头 cell 的 white-space 改为 normal + line-height:1.4。别名：HeadWrap。</summary>
        [HtmlAttributeName("HeaderWrap")]
        public bool HeaderWrap { get; set; }

        [HtmlAttributeName("HeadWrap")]
        public bool HeadWrap { get; set; }

        /// <summary>全局表头垂直对齐（top/middle/bottom，可被单个列的 HeaderVAlign/LabelVAlign 覆盖。别名：TitleVAlign。默认 middle。</summary>
        [HtmlAttributeName("HeaderVerticalAlign")]
        public string HeaderVerticalAlign { get; set; } = "middle";

        [HtmlAttributeName("HeaderVAlign")]
        public string HeaderVAlign { get; set; }

        [HtmlAttributeName("TitleVAlign")]
        public string TitleVAlign { get; set; }

        public override void Init(TagHelperContext context)
        {
            base.Init(context);
            var ha = string.IsNullOrWhiteSpace(TitleAlign) ? null : TitleAlign.Trim();
            if (!string.IsNullOrWhiteSpace(ha)) HeaderAlign = ha;
            var hva = string.IsNullOrWhiteSpace(TitleVAlign) ? (string.IsNullOrWhiteSpace(HeaderVAlign) ? null : HeaderVAlign.Trim()) : TitleVAlign.Trim();
            if (!string.IsNullOrWhiteSpace(hva)) HeaderVerticalAlign = hva;
            if (HeadWrap) HeaderWrap = true;
            context.Items[typeof(TableContext)] = new TableContext();
            context.Items["TableHeaderAlign"] = HeaderAlign;
            context.Items["TableHeaderVerticalAlign"] = HeaderVerticalAlign;
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            output.TagName = "div";
            AddCommonAttributes(context, output);
            string appId;
            lock (_idLock)
            {
                _idCounter++;
                appId = $"app-etbl-{_idCounter}";
            }
            output.Attributes.SetAttribute("id", appId);
            // FullHeight 模式（Height 已由 AddCommonAttributes 注入 style=height:xxx）：独立占视口，不再依赖父容器 h-full
            var rootStyle = output.Attributes["style"]?.Value?.ToString() ?? "";
            var hasExplicitHeight = !string.IsNullOrWhiteSpace(Height);
            if (hasExplicitHeight)
            {
                // ShowPage=false 且有显式 Height：不塞保底 min-height，避免与视口 calc 叠加产生底部空白
                if (ShowPage && !rootStyle.Contains("min-height"))
                    rootStyle += " min-height: 600px;";
                rootStyle += " width: 100%; box-sizing: border-box;";
                output.Attributes.SetAttribute("style", rootStyle.Trim());
                output.Attributes.SetAttribute("class", "w-full overflow-hidden flex flex-col bg-white");
            }
            else
            {
                output.Attributes.SetAttribute("class", "h-full min-h-[420px] flex flex-col overflow-hidden");
            }

            await output.GetChildContentAsync();
            var tableContext = (TableContext)context.Items[typeof(TableContext)];
            string toolbarHtml = tableContext.ToolbarHtml?.ToString();
            string scopeCssHtml = CreateHeaderScopeCss(appId);
            string tableHtml = CreateTable(tableContext);
            string footerHtml = CreateFooter();
            string scriptHtml = (this.BuildMode == EleAppBuildMode.Client) ? CreateScript(appId) : "";

            output.PreElement.AppendHtml(scopeCssHtml);
            // ShowPage=false 且 Height 显式设置时：去掉 footer 的 min-height 占位以消除底部空白
            bool showFooterSpace = ShowPage || !hasExplicitHeight;
            var containerCls = showFooterSpace
                ? " <el-container class='h-full w-full flex flex-col overflow-hidden'>"
                : " <el-container class='h-full w-full flex flex-col overflow-hidden' style='min-height:0;'>";
            output.Content.AppendHtml(containerCls);
            if (!string.IsNullOrWhiteSpace(toolbarHtml))
                output.Content.AppendHtml(toolbarHtml);
            output.Content.AppendHtml(tableHtml);
            output.Content.AppendHtml(footerHtml);
            output.Content.AppendHtml("    </el-container>");
            output.PostElement.AppendHtml(scriptHtml);
        }

        // 作用域 CSS：覆盖 Element Plus 的表头对齐与换行（放在 #app-etbl-N 根，避免被 Vue el-main 吞掉）
        private string CreateHeaderScopeCss(string appId)
        {
            var haVar = string.IsNullOrWhiteSpace(HeaderAlign) ? "center" : HeaderAlign;
            var hvaVar = string.IsNullOrWhiteSpace(HeaderVerticalAlign) ? "middle" : HeaderVerticalAlign;
            var scope = $"#{appId}";
            var wrap = HeaderWrap || HeadWrap;

            // 对齐映射：水平 justify-content（row 主轴），垂直垂直居中由 align-items:center 统一，多行用 align-content
            string Jc(string align) => (align ?? "center").ToLower() switch { "left" => "flex-start", "right" => "flex-end", _ => "center" };
            string Va(string align) => (align ?? "middle").ToLower() switch { "top" => "top", "bottom" => "bottom", _ => "middle" };

            var wrapStyles = wrap
                ? $"line-height: 1.4 !important; white-space: normal !important; word-break: break-word !important;"
                : $"white-space: nowrap !important; line-height: 1.2 !important;";

            var wrapFlex = wrap ? "flex-wrap: wrap !important;" : "flex-wrap: nowrap !important;";
            var caretMargin = haVar.Equals("left", StringComparison.OrdinalIgnoreCase) ? "0 0 0 4px"
                : haVar.Equals("right", StringComparison.OrdinalIgnoreCase) ? "0 4px 0 0"
                : "0 0 0 4px";

            return $@"<style>
{scope} th.el-table__cell {{ text-align: {haVar} !important; vertical-align: {Va(hvaVar)} !important; }}
{scope} th.el-table__cell .cell {{
  display: inline-flex !important; flex-direction: row !important;
  justify-content: {Jc(haVar)} !important; align-items: center !important; align-content: {Jc(hvaVar)} !important;
  width: 100% !important; min-height: 32px !important; box-sizing: border-box !important;
  padding: 6px 8px !important; height: 100% !important; text-align: {haVar} !important;
  column-gap: 6px !important;
  {wrapFlex}
  {wrapStyles}
}}
{scope} th.el-table__cell .cell > span {{ display: inline-flex; align-items: center; line-height: inherit; white-space: inherit; word-break: inherit; }}
{scope} th.el-table__cell .cell .caret-wrapper {{
  display: inline-flex !important; margin: {caretMargin} !important; flex: 0 0 auto !important; align-self: center !important;
  position: relative !important; top: auto !important; transform: none !important;
  width: 12px !important; height: 16px !important; flex-shrink: 0 !important;
}}
{scope} th.el-table__cell .cell .caret-wrapper .sort-caret {{
  position: absolute !important; left: 50% !important; transform: translateX(-50%) !important;
  width: 0 !important; height: 0 !important; border-style: solid !important;
}}
{scope} th.el-table__cell .cell .caret-wrapper .sort-caret.ascending {{
  top: 1px !important;
  border-width: 0 4px 4px 4px !important;
  border-color: transparent transparent currentColor transparent !important;
}}
{scope} th.el-table__cell .cell .caret-wrapper .sort-caret.descending {{
  bottom: 1px !important;
  border-width: 4px 4px 0 4px !important;
  border-color: currentColor transparent transparent transparent !important;
}}
{scope} th.el-table__cell .cell::after,
{scope} th.el-table__cell .cell::before {{ display: none !important; }}
</style>";
        }

        // 创建表格HTML，包含表头、数据行、选择列等
        private string CreateTable(TableContext tableContext)
        {
            var selectionCol = EnableBatch ? @"<el-table-column type=""selection"" width=""55"" fixed=""left""></el-table-column>" : "";
            var rowKeyAttr = !string.IsNullOrEmpty(RowKey) ? $@"row-key=""{RowKey}""" : "";
            var highlightAttr = !EnableBatch ? "highlight-current-row" : "";
            var selectionEvent = EnableBatch ? @"v-on:selection-change=""onSelectionChange""" : @"v-on:current-change=""onCurrentChange""";
            var defaultSortAttr = BuildDefaultSortAttr();
            var globalHeaderAlign = string.IsNullOrWhiteSpace(HeaderAlign) ? "" : $"header-align=\"{HeaderAlign}\"";
            var haVar = string.IsNullOrWhiteSpace(HeaderAlign) ? "center" : HeaderAlign;
            var wrap = HeaderWrap || HeadWrap;
            var headerWrapStyle = wrap
                ? " --el-table-header-cell-white-space: normal; --el-table-header-cell-line-height: 1.4; "
                : "";
            var tableHtml = $@"
        <el-main class=""flex-1 p-0 bg-white overflow-hidden flex flex-col"" style=""min-height: 0;"">
            <el-table
                :data=""items""
                border
                {globalHeaderAlign}
                {selectionEvent}
                v-on:sort-change=""onSortChange""
                height=""100%""
                style=""--tbl-ha: {haVar}; width: 100%; flex: 1; min-height: 0; --el-border-color: #909399; --el-table-border-color: #909399; --el-table-header-border-color: #909399; --el-table-row-border-color: #909399; --el-border-color-light: #a8abb2;{headerWrapStyle}""
                {rowKeyAttr}
                {highlightAttr}
                {defaultSortAttr}
                default-expand-all
            >
                {selectionCol}
                {tableContext.ColumnsHtml}
            </el-table>
        </el-main>
";
            return tableHtml;
        }

        // 2. Footer (Pagination) -> el-footer
        private string CreateFooter()
        {
            if (!ShowPage)
                // 无分页：彻底不留空白（无 min-height 占位），让 el-table flex:1 占满容器
                return @"
        <el-footer class=""flex-none p-0 bg-transparent"" style=""height:0;min-height:0;display:none""></el-footer>
";
            var defaultPageSize = ResolveDefaultPageSize();
            var pageSizeOptions = BuildPageSizeOptions(defaultPageSize);
            return $@"
        <el-footer class=""h-auto flex-none p-0 bg-white"">
                <div class=""py-1.5 px-4 flex items-center justify-between"">
                <div class=""text-gray-500 whitespace-nowrap"">共 {{{{ total }}}} 条</div>
                <div class=""flex items-center space-x-2"">
                    <span class=""text-gray-500 whitespace-nowrap"">每页记录数</span>
                    <el-select v-model=""pageSize"" class=""min-w-[80px] w-auto"" v-on:change=""handlePageSizeChange"" style=""max-width:120px;"">
                        {pageSizeOptions}
                    </el-select>
                    <el-pagination
                        background
                        layout=""prev, pager, next""
                        :total=""total""
                        :page-size=""pageSize""
                        :current-page=""pageIndex + 1""
                        v-on:current-change=""handlePageChange""
                    >
                    </el-pagination>
                </div>
            </div>
        </el-footer>
";
        }

        // 3. Script
        private string CreateScript(string appId)
        {
            var formDrawerSize = string.IsNullOrWhiteSpace(FormDrawerSize) ? "" : FormDrawerSize.Trim();
            var defaultPageSize = ResolveDefaultPageSize();
            var defaultSortField = this.SortField;
            var defaultSortDirection = ResolveSortDirection();
            return $@"
<script>
    (function() {{
        var MOUNT_ID = '#{appId}';
        var CONFIG = {{
            drawerTitle: '{Title}',
            dataHandler: '{DataHandler}',
            deleteHandler: '{DeleteHandler}',
            editPage: '{FormPage}',
            formDrawerSize: '{formDrawerSize}',
            pageSize: {defaultPageSize},
            defaultSortField: '{defaultSortField}',
            defaultSortDirection: '{defaultSortDirection}'
        }};
        function mountTable(retry) {{
            if (typeof retry === 'undefined') retry = 0;
            if (!window.EleTableAppBuilder) {{
                if (retry < 400) {{ setTimeout(function() {{ mountTable(retry + 1); }}, 25); return; }}
                console.error('EleTableAppBuilder 不可用，请确保 /_content/App.EleUI/eleui/eleui.js (ES module) 已成功加载。');
                return;
            }}
            try {{
                new window.EleTableAppBuilder().mount(MOUNT_ID, CONFIG);
            }} catch (err) {{
                console.error('EleTable mount(' + MOUNT_ID + ') 失败：', err);
            }}
        }}
        if (document.readyState === 'loading') {{
            document.addEventListener('DOMContentLoaded', function() {{ mountTable(); }}, {{ once: true }});
        }} else {{
            mountTable();
        }}
    }})();
</script>
";
        }

        /**构建默认排序属性 */
        private string BuildDefaultSortAttr()
        {
            var prop = this.SortField;
            if (string.IsNullOrWhiteSpace(prop))
                return "";
            var order = ResolveSortDirection().Equals("DESC", StringComparison.OrdinalIgnoreCase)  ? "descending" : "ascending";
            return $@":default-sort=""{{ prop: '{prop}', order: '{order}' }}""";
        }

        /**解析排序方向 */
        private string ResolveSortDirection()
        {
            var dir = string.IsNullOrWhiteSpace(SortDirection) ? "ASC" : SortDirection.Trim().ToUpperInvariant();
            return dir == "DESC" ? "DESC" : "ASC";
        }

        private int ResolveDefaultPageSize()
        {
            if (PageSize.GetValueOrDefault() > 0)
                return PageSize.Value;

            return 10;
        }

        private static string BuildPageSizeOptions(int defaultPageSize)
        {
            var values = new List<int> { 10, 20, 50, 100 };
            if (defaultPageSize > 0 && !values.Contains(defaultPageSize))
                values.Add(defaultPageSize);

            var seen = new HashSet<int>();
            var sb = new StringBuilder();

            foreach (var size in values)
            {
                if (size <= 0 || !seen.Add(size))
                    continue;

                sb.Append($@"<el-option :label=""{size}"" :value=""{size}""></el-option>");
            }

            return sb.ToString();
        }

    }
}
