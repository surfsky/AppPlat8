using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// EleForm 固定底部容器。内部控件默认居中、流式换行，并带最小高度。
    /// </summary>
    [HtmlTargetElement("Bottom", ParentTag = "EleForm")]
    public class EleBottomTagHelper : EleControlTagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();

            output.TagName = "div";
            output.Attributes.SetAttribute("data-ele-bottom", "true");
            output.Attributes.SetAttribute("class", "w-full min-h-[48px] flex flex-wrap items-center justify-center gap-2");
            output.Attributes.SetAttribute("style", "display:flex;flex-wrap:wrap;align-items:center;justify-content:center;gap:8px;width:100%;min-height:48px;");
            AddCommonAttributes(context, output);
            output.Content.SetHtmlContent(content);
        }
    }
}
