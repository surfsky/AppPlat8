using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// EleTable 专用工具栏容器。
    /// </summary>
    [HtmlTargetElement("Toolbar", ParentTag = "EleTable")]
    public class EleToolbarTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();
                const string toolbarClass = "ele-table-toolbar w-full flex-none shrink-0 min-h-[40px] flex flex-wrap items-center gap-2 bg-white px-6 py-4 overflow-visible relative z-10";
                const string toolbarStyle = "display:flex;flex-wrap:wrap;align-items:center;gap:8px;width:100%;min-height:40px;flex-shrink:0;overflow:visible;position:relative;z-index:10;";

            var tableContext = (TableContext)context.Items[typeof(TableContext)];
            var wrapper = $@"<div class=""{toolbarClass}"" style=""{toolbarStyle}"" data-ele-toolbar=""true"">{content}</div>";
            tableContext.ToolbarHtml.Append(wrapper);
            output.SuppressOutput();
        }
    }
}
