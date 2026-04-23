using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// Grid 容器。支持响应式列数：Cols / SmallCols / MiddleCols / LargeCols / XLargeCols。
    /// </summary>
    [HtmlTargetElement("Grid")]
    public class EleLayoutGridTagHelper : EleItemTagHelper
    {
        [HtmlAttributeName("Cols")]
        public int Cols { get; set; } = 1;

        [HtmlAttributeName("SmallCols")]
        public int? SmallCols { get; set; }

        [HtmlAttributeName("MiddleCols")]
        public int? MiddleCols { get; set; }

        [HtmlAttributeName("LargeCols")]
        public int? LargeCols { get; set; }

        [HtmlAttributeName("XLargeCols")]
        public int? XLargeCols { get; set; }

        [HtmlAttributeName("Gap")]
        public string Gap { get; set; } = "4";

        [HtmlAttributeName("MaxWidth")]
        public string MaxWidth { get; set; } = "7xl";

        [HtmlAttributeName("Center")]
        public bool Center { get; set; } = true;

        [HtmlAttributeName("PaddingX")]
        public string PaddingX { get; set; } = "4";

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "div";
            AddCommonAttributes(context, output);

            var gridClass = "grid";
            if (Cols > 0)
                gridClass += " grid-cols-" + Cols;
            if (SmallCols.HasValue && SmallCols.Value > 0)
                gridClass += " sm:grid-cols-" + SmallCols.Value;
            if (MiddleCols.HasValue && MiddleCols.Value > 0)
                gridClass += " md:grid-cols-" + MiddleCols.Value;
            if (LargeCols.HasValue && LargeCols.Value > 0)
                gridClass += " lg:grid-cols-" + LargeCols.Value;
            if (XLargeCols.HasValue && XLargeCols.Value > 0)
                gridClass += " xl:grid-cols-" + XLargeCols.Value;

            if (!string.IsNullOrWhiteSpace(Gap))
                gridClass += " gap-" + Gap;
            if (!string.IsNullOrWhiteSpace(MaxWidth))
                gridClass += MaxWidth.StartsWith("max-w-") ? " " + MaxWidth : " max-w-" + MaxWidth;
            if (Center)
                gridClass += " mx-auto";
            if (!string.IsNullOrWhiteSpace(PaddingX))
                gridClass += " px-" + PaddingX;

            output.Attributes.SetAttribute("class", ComposeClass(output, gridClass));
        }
    }
}