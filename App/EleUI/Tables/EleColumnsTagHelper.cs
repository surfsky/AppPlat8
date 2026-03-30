using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>表格列定义标签助手。</summary>
    [HtmlTargetElement("Columns", ParentTag = "EleTable")]
    public class EleColumnsTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var tableContext = (TableContext)context.Items[typeof(TableContext)];
            tableContext.ColumnsHtml.Append(childContent.GetContent());
            output.SuppressOutput();
        }
    }
}
