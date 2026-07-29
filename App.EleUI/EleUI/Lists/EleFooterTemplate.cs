using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// EleList 的底部模板。
    /// 渲染在列表底部"没有更多数据了"行的右侧，可包含按钮或其他自定义内容。
    /// </summary>
    [HtmlTargetElement("Footer", ParentTag = "EleList")]
    public class EleFooterTemplate : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var child = await output.GetChildContentAsync();
            var listContext = (ListContext)context.Items[typeof(ListContext)];
            listContext.FooterTemplateHtml.Append(child.GetContent());
            output.SuppressOutput();
        }
    }
}
