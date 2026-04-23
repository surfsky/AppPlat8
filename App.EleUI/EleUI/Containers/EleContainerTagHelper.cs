using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Globalization;

namespace App.EleUI
{
    /// <summary>
    /// 容器组件。提供接近 Bootstrap 的响应式容器行为。
    /// 默认宽度规则：
    /// - &lt;576px: 100%
    /// - >=768px: 768px
    /// - >=992px: 992px
    /// - >=1200px: 1200px
    /// - >=1400px: 1400px
    /// </summary>
    [HtmlTargetElement("EleContainer")]
    public class EleContainerTagHelper : EleItemTagHelper
    {
        /// <summary>是否水平居中（mx-auto）。默认 true。</summary>
        [HtmlAttributeName("Center")]
        public bool Center { get; set; } = true;

        /// <summary>是否启用 Bootstrap 风格响应式宽度。默认 true。</summary>
        [HtmlAttributeName("Responsive")]
        public bool Responsive { get; set; } = true;

        /// <summary>水平内边距（px-）。默认 "4"。</summary>
        [HtmlAttributeName("PaddingX")]
        public string PaddingX { get; set; } = "4";

        /// <summary>外边距（m-）。示例："8" 对应 "m-8"。</summary>
        [HtmlAttributeName("Margin")]
        public string Margin { get; set; }

        /// <summary>md 断点固定宽度。默认 768px。</summary>
        [HtmlAttributeName("MdWidth")]
        public string MdWidth { get; set; } = "768px";

        /// <summary>lg 断点固定宽度。默认 992px。</summary>
        [HtmlAttributeName("LgWidth")]
        public string LgWidth { get; set; } = "992px";

        /// <summary>xl 断点固定宽度。默认 1200px。</summary>
        [HtmlAttributeName("XlWidth")]
        public string XlWidth { get; set; } = "1200px";

        /// <summary>xxl 断点固定宽度。默认 1400px。</summary>
        [HtmlAttributeName("XxlWidth")]
        public string XxlWidth { get; set; } = "1400px";

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "div";
            AddCommonAttributes(context, output);

            var containerClass = "w-full";

            if (Responsive)
            {
                var responsiveClass = $"ele-container-{SanitizeId(context.UniqueId)}";
                containerClass += " " + responsiveClass;

                var mdWidth = NormalizeWidth(MdWidth, "768px");
                var lgWidth = NormalizeWidth(LgWidth, "992px");
                var xlWidth = NormalizeWidth(XlWidth, "1200px");
                var xxlWidth = NormalizeWidth(XxlWidth, "1400px");

                output.PostElement.AppendHtml($@"<style>
.{responsiveClass} {{ width: 100%; }}
@media (min-width: 576px) {{ .{responsiveClass} {{ width: 100%; }} }}
@media (min-width: 768px) {{ .{responsiveClass} {{ width: {mdWidth} !important; }} }}
@media (min-width: 992px) {{ .{responsiveClass} {{ width: {lgWidth} !important; }} }}
@media (min-width: 1200px) {{ .{responsiveClass} {{ width: {xlWidth} !important; }} }}
@media (min-width: 1400px) {{ .{responsiveClass} {{ width: {xxlWidth} !important; }} }}
</style>");
            }

            // 居中
            if (Center)
                containerClass += " mx-auto";

            // 水平内边距
            if (!string.IsNullOrWhiteSpace(PaddingX))
                containerClass += " px-" + PaddingX.Trim();

            // 单独的外边距（如果提供）
            if (!string.IsNullOrWhiteSpace(Margin))
                containerClass += " m-" + Margin.Trim();

            output.Attributes.SetAttribute("class", ComposeClass(output, containerClass));
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "container";

            return value.Replace("-", "_").Replace(":", "_");
        }

        private static string NormalizeWidth(string width, string fallback)
        {
            if (string.IsNullOrWhiteSpace(width))
                return fallback;

            var w = width.Trim();
            if (w.EndsWith("px") || w.EndsWith("rem") || w.EndsWith("%") || w.EndsWith("vw"))
                return w;

            return double.TryParse(w, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                ? $"{value}px"
                : fallback;
        }
    }
}
