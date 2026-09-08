using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using App.Utils;

namespace App.EleUI
{
    /// <summary>
    /// EleTable 工具栏按钮区。独立一行展示按钮。
    /// </summary>
    [HtmlTargetElement("Buttons", ParentTag = "Toolbar")]
    public class EleButtons : TagHelper
    {
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();

            if (IsSelectMode())
            {
                // Select 模式下，若用户显式写了 Select 按钮（<EleButton Command="Select">选择</EleButton>），则不再自动追加
                var hasExplicitSelect = !string.IsNullOrWhiteSpace(content) && (
                    content.Contains("选择") ||
                    content.Contains("invokeCommand", StringComparison.OrdinalIgnoreCase) && (content.Contains("'Select'") || content.Contains("\"Select\"") || content.Contains("Select")) ||
                    content.Contains("Command=\"Select\"", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("Command='Select'", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("v-on:click=\"invokeCommand('Select')\"", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("v-on:click=\"invokeCommand(\\\"Select\\\")\"", StringComparison.OrdinalIgnoreCase));
                if (!hasExplicitSelect)
                {
                    content += "<el-button type='success' v-on:click=\"invokeCommand('Select')\">选择</el-button>";
                }
            }

            output.TagName = null;
            output.Content.SetHtmlContent($@"
<div class='ele-table-buttons-block w-auto md:w-full shrink-0 flex items-center gap-1.5 flex-nowrap md:flex-wrap overflow-x-auto md:overflow-visible'>
    {content}
</div>");
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
