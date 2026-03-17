using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Linq;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("Toolbar")]
    public class ToolbarTagHelper : TagHelper
    {
        [HtmlAttributeName("Position")]
        public string Position { get; set; } = "top"; // future use

        [HtmlAttributeName("Padding")]
        public string Padding { get; set; }

        [HtmlAttributeName("Bg")]
        public string Bg { get; set; }

        [HtmlAttributeName("FillRow")]
        public bool FillRow { get; set; } = true; // defaults to true for full width

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            // Mark context so EleFormTagHelper can detect toolbar content
            context.Items["HasToolbar"] = true;

            output.TagName = "div";
            output.Attributes.SetAttribute("data-ele-toolbar", "true");
            output.Attributes.SetAttribute("data-toolbar-position", (Position ?? "top").ToLower());
            var style = "display:flex;flex-direction:row;align-items:center;justify-content:flex-start;gap:8px;";
            if (!string.IsNullOrEmpty(Padding)) style += $" padding:{Padding};";
            if (!string.IsNullOrEmpty(Bg)) style += $" background:{Bg};";
            output.Attributes.SetAttribute("style", style);

            // Add FillRow class to make toolbar span full width
            if (FillRow)
            {
                var cssClass = output.Attributes.FirstOrDefault(a => a.Name == "class")?.Value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(cssClass))
                    cssClass += " col-span-full";
                else
                    cssClass = "col-span-full";
                output.Attributes.SetAttribute("class", cssClass);
            }

            var child = await output.GetChildContentAsync();
            output.Content.SetHtmlContent(child.GetContent());
        }
    }
}
