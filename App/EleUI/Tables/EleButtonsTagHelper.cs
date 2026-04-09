using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// EleTable 工具栏按钮区。独立一行展示按钮。
    /// </summary>
    [HtmlTargetElement("Buttons", ParentTag = "Toolbar")]
    public class EleButtonsTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();

            output.TagName = null;
            output.Content.SetHtmlContent($@"
<div class='ele-table-buttons-block w-auto md:w-full shrink-0 flex items-center gap-2 flex-nowrap md:flex-wrap overflow-x-auto md:overflow-visible'>
    {content}
</div>");
        }
    }
}
