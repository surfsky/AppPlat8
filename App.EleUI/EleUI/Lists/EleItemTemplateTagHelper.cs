using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// EleList 的条目模板。
    /// </summary>
    [HtmlTargetElement("ItemTemplate", ParentTag = "EleList")]
    public class EleItemTemplateTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var child = await output.GetChildContentAsync();
            var listContext = (ListContext)context.Items[typeof(ListContext)];
            listContext.ItemTemplateHtml.Append(child.GetContent());
            output.SuppressOutput();
        }
    }
}