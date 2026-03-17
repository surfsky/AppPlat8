using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// 列容器。默认输出 flex flex-col，并支持常见对齐与间距。
    /// </summary>
    [HtmlTargetElement("Column")]
    public class EleLayoutColumnTagHelper : EleItemTagHelper
    {
        [HtmlAttributeName("Gap")]
        public string Gap { get; set; } = "4";

        [HtmlAttributeName("Items")]
        public string Items { get; set; }

        [HtmlAttributeName("Justify")]
        public string Justify { get; set; }

        [HtmlAttributeName("Reverse")]
        public bool Reverse { get; set; }

        [HtmlAttributeName("Grow")]
        public bool Grow { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "div";
            AddCommonAttributes(context, output);

            var colClass = Reverse ? "flex flex-col-reverse" : "flex flex-col";
            if (!string.IsNullOrWhiteSpace(Gap))
                colClass += " gap-" + Gap;
            if (!string.IsNullOrWhiteSpace(Items))
                colClass += " items-" + Items;
            if (!string.IsNullOrWhiteSpace(Justify))
                colClass += " justify-" + Justify;
            if (Grow)
                colClass += " flex-1";

            output.Attributes.SetAttribute("class", ComposeClass(output, colClass));
        }
    }
}