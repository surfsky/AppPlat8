using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// 行容器。默认输出与 "flex flex-wrap gap-2" 一致的布局类。
    /// </summary>
    [HtmlTargetElement("Row")]
    public class EleLayoutRowTagHelper : EleItemTagHelper
    {
        [HtmlAttributeName("Wrap")]
        public bool Wrap { get; set; } = true;

        [HtmlAttributeName("Gap")]
        public string Gap { get; set; } = "2";

        [HtmlAttributeName("Items")]
        public string Items { get; set; }

        [HtmlAttributeName("Justify")]
        public string Justify { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "div";
            AddCommonAttributes(context, output);

            var rowClass = "flex";
            if (Wrap)
                rowClass += " flex-wrap";
            if (!string.IsNullOrWhiteSpace(Gap))
                rowClass += " gap-" + Gap;
            if (!string.IsNullOrWhiteSpace(Items))
                rowClass += " items-" + Items;
            if (!string.IsNullOrWhiteSpace(Justify))
                rowClass += " justify-" + Justify;

            output.Attributes.SetAttribute("class", ComposeClass(output, rowClass));
        }
    }
}