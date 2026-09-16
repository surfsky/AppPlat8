using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>编号列标签助手。</summary>
    [HtmlTargetElement("EleNumColumn", ParentTag = "Columns")]
    [HtmlTargetElement("EleNumColumn", ParentTag = "EleColumnGroup")]
    public class EleNumColumn : EleColumnBase
    {
        public EleNumColumn()
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
            var tableHeaderAlign = context.Items.ContainsKey("TableHeaderAlign") ? context.Items["TableHeaderAlign"] as string : null;
            ApplyBaseColumnAttributes(output, null, tableHeaderAlign);
            output.Attributes.SetAttribute("type", "index");
        }
    }
}
