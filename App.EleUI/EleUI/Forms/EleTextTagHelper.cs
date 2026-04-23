using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// 强类型文本控件。默认输出 div 包裹，可通过 Wrapper 为空关闭包裹。
    /// 示例：<Text For="Item.Name" class="text-sm" />
    /// 示例：<Text For="Item.Name" Wrapper="" />
    /// </summary>
    [HtmlTargetElement("Text")]
    public class EleTextTagHelper : TagHelper
    {
        [HtmlAttributeName("For")]
        public ModelExpression For { get; set; }

        [HtmlAttributeName("Wrapper")]
        public string Wrapper { get; set; } = "div";

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var rawPath = For?.Name;
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                output.SuppressOutput();
                return;
            }

            var expr = BuildClientExpression(rawPath);
            var textHtml = $"{{{{ {expr} }}}}";
            var wrapper = Wrapper?.Trim();

            if (string.IsNullOrWhiteSpace(wrapper))
            {
                output.TagName = null;
                output.Content.SetHtmlContent(textHtml);
                return;
            }

            output.TagName = wrapper;
            output.TagMode = TagMode.StartTagAndEndTag;
            output.Content.SetHtmlContent(textHtml);
        }

        private static string BuildClientExpression(string path)
        {
            var normalized = path.Trim();

            if (normalized.StartsWith("Model."))
                normalized = normalized.Substring("Model.".Length);

            if (normalized.StartsWith("Item."))
                normalized = "item." + normalized.Substring("Item.".Length);

            var parts = normalized.Split('.');
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;
                var p = parts[i];
                if (char.IsUpper(p[0]))
                    parts[i] = char.ToLowerInvariant(p[0]) + p.Substring(1);
            }

            return string.Join('.', parts);
        }
    }
}
