using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>编号列标签助手。</summary>
    [HtmlTargetElement("EleNumColumn", ParentTag = "Columns")]
    public class EleNumColumnTagHelper : EleColumnBaseTagHelper
    {
        public EleNumColumnTagHelper()
        {
            Label = "#";
            Width = "50";
            Align = "center";
            Sortable = false;
            Resizable = false;
        }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckVisible(output))
                return;

            SetupColumnShell(output);
            ApplyBaseColumnAttributes(output);
            output.Attributes.SetAttribute("type", "index");
        }
    }
}
