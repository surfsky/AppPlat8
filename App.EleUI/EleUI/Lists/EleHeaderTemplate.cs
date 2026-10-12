using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// EleList 的表头模板。
    /// 渲染在滚动容器内部顶部，使用 sticky 固顶，可用于放置与数据行列对齐的表头。
    /// </summary>
    [HtmlTargetElement("Header", ParentTag = "EleList")]
    public class EleHeaderTemplate : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var child = await output.GetChildContentAsync();
            var listContext = (ListContext)context.Items[typeof(ListContext)];
            listContext.HeaderTemplateHtml.Append(child.GetContent());
            output.SuppressOutput();
        }
    }
}
