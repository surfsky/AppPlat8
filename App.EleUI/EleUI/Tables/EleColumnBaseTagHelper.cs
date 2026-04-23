using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>表格列基础标签助手，封装列的通用属性。</summary>
    public abstract class EleColumnBaseTagHelper : TagHelper
    {
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        [HtmlAttributeName("Label")]
        public string Label { get; set; }

        [HtmlAttributeName("Width")]
        public string Width { get; set; }

        [HtmlAttributeName("Size")]
        public string Size { get; set; }

        [HtmlAttributeName("MinWidth")]
        public string MinWidth { get; set; }

        [HtmlAttributeName("Sortable")]
        public bool? Sortable { get; set; }

        [HtmlAttributeName("Resizable")]
        public bool? Resizable { get; set; }

        [HtmlAttributeName("Visible")]
        public bool Visible { get; set; } = true;

        [HtmlAttributeName("Fixed")]
        public string Fixed { get; set; }

        [HtmlAttributeName("Align")]
        public string Align { get; set; }

        protected bool CheckVisible(TagHelperOutput output)
        {
            if (!Visible)
            {
                output.SuppressOutput();
                return false;
            }

            return true;
        }

        protected void SetupColumnShell(TagHelperOutput output)
        {
            output.TagName = "el-table-column";
            output.TagMode = TagMode.StartTagAndEndTag;
        }

        protected void ApplyBaseColumnAttributes(TagHelperOutput output, string labelOverride = null)
        {
            var label = labelOverride ?? Label;
            if (!string.IsNullOrWhiteSpace(label))
                output.Attributes.SetAttribute("label", label);

            var width = !string.IsNullOrWhiteSpace(Width) ? Width : Size;
            if (!string.IsNullOrWhiteSpace(width))
                output.Attributes.SetAttribute("width", width);

            if (!string.IsNullOrWhiteSpace(MinWidth))
                output.Attributes.SetAttribute("min-width", MinWidth);

            if (Sortable.HasValue)
                output.Attributes.SetAttribute("sortable", Sortable.Value ? "custom" : "false");

            if (Resizable.HasValue)
                output.Attributes.SetAttribute(":resizable", Resizable.Value.ToString().ToLower());

            if (!string.IsNullOrWhiteSpace(Fixed))
                output.Attributes.SetAttribute("fixed", Fixed);

            if (!string.IsNullOrWhiteSpace(Align))
                output.Attributes.SetAttribute("align", Align);
        }
    }
}
