using App.DAL;
using App.Components;
using System;
using System.Reflection;
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
    public class EleTableTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("Title")]
        public string Title { get; set; } = "列表";

        [HtmlAttributeName("FormPage")]
        public string FormPage { get; set; }

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

        public override void Init(TagHelperContext context)
        {
            base.Init(context);
            context.Items[typeof(TableContext)] = new TableContext();
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            output.TagName = "div";
            AddCommonAttributes(context, output);
            output.Attributes.SetAttribute("id", "app");  // 用于挂载 Vue 应用，有潜在id冲突问题
            output.Attributes.SetAttribute("class", "h-full flex flex-col overflow-hidden");

            // 编撰表格网页面结构，包含工具栏、表格、分页、弹窗等
            await output.GetChildContentAsync(); // Execute children to populate TableContext
            var tableContext = (TableContext)context.Items[typeof(TableContext)];
            string toolbarHtml = tableContext.ToolbarHtml?.ToString();
            string tableHtml = CreateTable(tableContext);
            string footerHtml = CreateFooter();
            string scriptHtml = (this.BuildMode == EleAppBuildMode.Client) ? CreateScript() : "";

            output.Content.AppendHtml(" <el-container class='h-full w-full flex flex-col overflow-hidden'>");
            output.Content.AppendHtml(toolbarHtml);
            output.Content.AppendHtml(tableHtml);
            output.Content.AppendHtml(footerHtml);
            output.Content.AppendHtml("    </el-container>");
            output.Content.AppendHtml(scriptHtml);
        }

        // 创建表格HTML，包含表头、数据行、选择列等
        private string CreateTable(TableContext tableContext)
        {
            var selectionCol = EnableBatch ? @"<el-table-column type=""selection"" width=""55""></el-table-column>" : "";
            var rowKeyAttr = !string.IsNullOrEmpty(RowKey) ? $@"row-key=""{RowKey}""" : "";
            var highlightAttr = !EnableBatch ? "highlight-current-row" : "";
            var selectionEvent = EnableBatch ? @"v-on:selection-change=""onSelectionChange""" : @"v-on:current-change=""onCurrentChange""";
            var tableHtml = $@"
        <el-main class=""flex-1 p-0 bg-white overflow-hidden flex flex-col"">
            <el-table
                :data=""items""
                border
                {selectionEvent}
                v-on:sort-change=""onSortChange""
                height=""100%""
                style=""width: 100%; flex: 1;""
                {rowKeyAttr}
                {highlightAttr}
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
        private static string CreateFooter()
        {
            return $@"
        <el-footer class=""h-auto flex-none p-0 bg-white"">
            <div class=""py-3 px-4 flex items-center justify-between"">
                <div class=""text-gray-500 whitespace-nowrap"">共 {{{{ total }}}} 条</div>
                <div class=""flex items-center space-x-2"">
                    <span class=""text-gray-500 whitespace-nowrap"">每页记录数</span>
                    <el-select v-model=""pageSize"" class=""min-w-[80px] w-auto"" v-on:change=""handlePageSizeChange"" style=""max-width:120px;"">
                        <el-option :label=""10"" :value=""10""></el-option>
                        <el-option :label=""20"" :value=""20""></el-option>
                        <el-option :label=""50"" :value=""50""></el-option>
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
        private string CreateScript()
        {
            return $@"
<script>
    document.addEventListener('DOMContentLoaded', function() {{
        new EleTableAppBuilder().mount('#app', {{
            drawerTitle: '{Title}',
            dataHandler: '{DataHandler}',
            deleteHandler: '{DeleteHandler}',
            editPage: '{FormPage}',
            pageSize: 10
        }});
    }});
</script>
";
        }

    }
}
