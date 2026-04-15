using System.Threading.Tasks;
using System;
using App.Utils;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// EleTable 专用工具栏容器。
    /// </summary>
    [HtmlTargetElement("Toolbar", ParentTag = "EleTable")]
    public class EleToolbarTagHelper : TagHelper
    {
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();

            if (IsSelectMode() && !content.Contains("ele-table-buttons-block", StringComparison.OrdinalIgnoreCase))
            {
                content += @"<div class='ele-table-buttons-block w-auto md:w-full shrink-0 flex items-center gap-2 flex-nowrap md:flex-wrap overflow-x-auto md:overflow-visible'>
    <el-button type='success' v-on:click=""invokeCommand('Select')"">选择</el-button>
</div>";
            }
                const string toolbarClass = "ele-table-toolbar w-full flex-none shrink-0 min-h-[40px] flex flex-wrap items-center gap-2 bg-white px-6 py-4 overflow-visible relative z-10";
                const string toolbarStyle = "display:flex;flex-wrap:wrap;align-items:center;gap:8px;width:100%;min-height:40px;flex-shrink:0;overflow:visible;position:relative;z-index:10;";

            var tableContext = (TableContext)context.Items[typeof(TableContext)];
            var wrapper = $@"<div class=""{toolbarClass}"" style=""{toolbarStyle}"" data-ele-toolbar=""true"">{content}</div>";
            tableContext.ToolbarHtml.Append(wrapper);
            output.SuppressOutput();
        }

        private bool IsSelectMode()
        {
            var md = ViewContext?.HttpContext?.Request?.Query["md"].ToString();
            if (string.IsNullOrWhiteSpace(md))
                return false;

            if (Enum.TryParse<PageMode>(md, true, out var mode))
                return mode == PageMode.Select;

            if (int.TryParse(md, out var modeValue))
                return ((PageMode)modeValue) == PageMode.Select;

            return string.Equals(md, "select", StringComparison.OrdinalIgnoreCase);
        }
    }
}
