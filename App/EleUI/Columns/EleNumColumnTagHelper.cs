using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>编号列标签助手。</summary>
    [HtmlTargetElement("EleNumColumn")]
    public class EleNumColumnTagHelper : TagHelper
    {
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "el-table-column";
            output.TagMode = TagMode.StartTagAndEndTag; // Force full closing tag
            output.Attributes.SetAttribute("type", "index");
            output.Attributes.SetAttribute("label", "#");
            output.Attributes.SetAttribute("width", "50");
            output.Attributes.SetAttribute("align", "center");
        }
    }
}
