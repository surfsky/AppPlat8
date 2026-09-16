using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>表格列分组（多级表头），允许嵌套 EleColumn 或 EleColumnGroup。</summary>
    [HtmlTargetElement("EleColumnGroup", ParentTag = "Columns")]
    [HtmlTargetElement("EleColumnGroup", ParentTag = "EleColumnGroup")]
    [RestrictChildren("EleColumn", "EleColumnGroup", "EleLinkColumn", "EleImageColumn", "EleIconColumn", "EleNumColumn", "EleOpColumn")]
    public class EleColumnGroup : TagHelper
    {
        [HtmlAttributeName("Label")]   public string Label   { get; set; }
        [HtmlAttributeName("Align")]   public string Align   { get; set; }
        [HtmlAttributeName("HeaderAlign")] public string HeaderAlign { get; set; }
        [HtmlAttributeName("LabelAlign")]  public string LabelAlign  { get; set; }
        [HtmlAttributeName("Width")]   public string Width   { get; set; }
        [HtmlAttributeName("MinWidth")] public string MinWidth { get; set; }
        [HtmlAttributeName("Fixed")]   public string Fixed   { get; set; }
        [HtmlAttributeName("Visible")] public bool Visible   { get; set; } = true;
        [HtmlAttributeName("Sortable")] public bool? Sortable { get; set; }
        [HtmlAttributeName("Resizable")] public bool? Resizable { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!Visible) { output.SuppressOutput(); return; }
            var tableHeaderAlign = context.Items.ContainsKey("TableHeaderAlign") ? context.Items["TableHeaderAlign"] as string : null;

            output.TagName = "el-table-column";
            output.TagMode = TagMode.StartTagAndEndTag;

            if (!string.IsNullOrWhiteSpace(Label))    output.Attributes.SetAttribute("label", Label);
            if (!string.IsNullOrWhiteSpace(Align))    output.Attributes.SetAttribute("align", Align);

            // Group header align: explicit HeaderAlign/LabelAlign > table default
            var ha = HeaderAlign ?? LabelAlign ?? tableHeaderAlign;
            if (!string.IsNullOrWhiteSpace(ha))
                output.Attributes.SetAttribute("header-align", ha);

            if (!string.IsNullOrWhiteSpace(Width))    output.Attributes.SetAttribute("width", Width);
            if (!string.IsNullOrWhiteSpace(MinWidth)) output.Attributes.SetAttribute("min-width", MinWidth);
            if (!string.IsNullOrWhiteSpace(Fixed))    output.Attributes.SetAttribute("fixed", Fixed);

            if (Sortable.HasValue)
                output.Attributes.SetAttribute(":sortable", Sortable.Value ? "'custom'" : "false");
            if (Resizable.HasValue)
                output.Attributes.SetAttribute(":resizable", Resizable.Value.ToString().ToLower());

            var childContent = await output.GetChildContentAsync();
            output.Content.SetHtmlContent(childContent);
        }
    }
}
