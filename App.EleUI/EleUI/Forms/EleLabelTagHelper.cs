using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// Element Plus Form Label / Section Header
    /// Renders a full-width label or section divider
    /// Supports For expression for automatic label extraction
    /// </summary>
    [HtmlTargetElement("EleLabel")]
    public class EleLabelTagHelper : EleFormControlTagHelper
    {
        // FillRow is now in base class
        public EleLabelTagHelper()
        {
            FillRow = true; // Default true for Label
        }

        [HtmlAttributeName("Color")]
        public string Color { get; set; }

        [HtmlAttributeName("Size")]
        public string Size { get; set; } = "16px";

        [HtmlAttributeName("Bold")]
        public bool Bold { get; set; } = true;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            // Auto-set Label from For expression if not provided
            TryAutoSetLabel();
            
            output.TagName = "div";
            output.TagMode = TagMode.StartTagAndEndTag;
            
            var style = "margin-bottom: 10px; margin-top: 10px;";
            if (Bold)
            {
                style += " font-weight: bold;";
            }
            if (!string.IsNullOrEmpty(Size))
            {
                style += $" font-size: {Size};";
            }
            if (!string.IsNullOrEmpty(Color))
            {
                style += $" color: {Color};";
            }
            if (!string.IsNullOrEmpty(Width))
            {
                style += $" width: {Width};";
            }
            
            output.Attributes.SetAttribute("style", style);
            
            // If FillRow is true (default), wrap in el-col :span="24"
            // The grid system in EleForm uses grid-cols, so we should use col-span-full
            if (FillRow)
            {
                output.PreElement.SetHtmlContent(@"<div class=""col-span-full"">");
                output.PostElement.SetHtmlContent(@"</div>");
            }
            
            output.Content.SetContent(Label);
            
            // If child content exists, append it? Usually EleLabel is self-closing or empty content.
            // But if user writes <EleLabel>Text</EleLabel>, we should respect it.
            var childContent = await output.GetChildContentAsync();
            if (!childContent.IsEmptyOrWhiteSpace)
            {
                output.Content.SetHtmlContent(childContent);
            }
        }
    }
}
