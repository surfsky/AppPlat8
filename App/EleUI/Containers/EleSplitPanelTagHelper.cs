using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Net;
using System;
using System.Globalization;

namespace App.EleUI
{
    /// <summary>
    /// 分隔面板布局方向
    /// </summary>
    public enum SplitDirection
    {
        Horizontal,
        Vertical
    }

    /// <summary>
    /// 分隔面板组件
    /// </summary>
    [HtmlTargetElement("EleSplitPanel")]
    public class EleSplitPanelTagHelper : EleControlTagHelper
    {
        /// <summary>Layout direction of the splitter (horizontal / vertical). Default: horizontal</summary>
        [HtmlAttributeName("Direction")]
        public SplitDirection Direction { get; set; } = SplitDirection.Horizontal;

        /// <summary>Whether to enable lazy mode. Default: false</summary>
        [HtmlAttributeName("Lazy")]
        public bool Lazy { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            output.TagName = "el-splitter";
            AddCommonAttributes(context, output);

            output.Attributes.SetAttribute("layout", Direction.ToString().ToLower());
            if (Lazy) output.Attributes.SetAttribute("lazy", "true");

            // Element Plus splitter needs explicit height often, handled by EleTagHelper.Height -> style
        }
    }

    [HtmlTargetElement("EleSplitPanelItem")]
    [HtmlTargetElement("PanelItem", ParentTag = "EleSplitPanel")]
    public class EleSplitPanelItemTagHelper : EleControlTagHelper
    {
        /// <summary>Size of the panel (in pixels or percentage)</summary>
        [HtmlAttributeName("Size")]
        public string Size { get; set; }

        /// <summary>Minimum size of the panel</summary>
        [HtmlAttributeName("Min")]
        public string Min { get; set; }

        /// <summary>Maximum size of the panel</summary>
        [HtmlAttributeName("Max")]
        public string Max { get; set; }

        /// <summary>Whether the panel can be resized</summary>
        [HtmlAttributeName("Resizable")]
        public bool? Resizable { get; set; }

        /// <summary>Whether the panel can be collapsed，Default: false</summary>
        [HtmlAttributeName("Collapsible")]
        public bool Collapsible { get; set; }

        /// <summary>When provided, panel renders a full-size iframe automatically.</summary>
        [HtmlAttributeName("IframeUrl")]
        public string IframeUrl { get; set; }

        /// <summary>Alias of IframeUrl for compatibility.</summary>
        [HtmlAttributeName("IFrameUrl")]
        public string IFrameUrl
        {
            get => IframeUrl;
            set => IframeUrl = value;
        }

        /// <summary>Optional iframe name for target-based navigation.</summary>
        [HtmlAttributeName("IframeName")]
        public string IframeName { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            output.TagName = "el-splitter-panel";
            AddCommonAttributes(context, output);

            var sizeExpr = BuildPanelSizeExpression(Size);
            var minExpr = BuildPanelSizeExpression(Min);
            var maxExpr = BuildPanelSizeExpression(Max);

            if (!string.IsNullOrEmpty(sizeExpr)) output.Attributes.SetAttribute(":size", sizeExpr);
            if (!string.IsNullOrEmpty(minExpr))  output.Attributes.SetAttribute(":min", minExpr);
            if (!string.IsNullOrEmpty(maxExpr))  output.Attributes.SetAttribute(":max", maxExpr);
            if (Resizable == false)          output.Attributes.SetAttribute(":resizable", "false");
            if (Collapsible)                 output.Attributes.SetAttribute(":collapsible", "true");

            if (!string.IsNullOrWhiteSpace(IframeUrl))
            {
                var iframeUrl = WebUtility.HtmlEncode(IframeUrl.Trim());
                var iframeName = string.IsNullOrWhiteSpace(IframeName)
                    ? string.Empty
                    : $" name=\"{WebUtility.HtmlEncode(IframeName.Trim())}\"";
                output.Content.SetHtmlContent($"<iframe{iframeName} src=\"{iframeUrl}\" class=\"w-full h-full border-0\" frameborder=\"0\"></iframe>");
            }
        }

        private string BuildPanelSizeExpression(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var value = raw.Trim();

            // `200` => `200px`
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var pixelValue))
            {
                var normalized = pixelValue % 1 == 0
                    ? ((long)pixelValue).ToString(CultureInfo.InvariantCulture)
                    : pixelValue.ToString("0.###", CultureInfo.InvariantCulture);
                return $"'{EscapeJs(normalized)}px'";
            }

            // `30%` keeps percentage; `200px` keeps pixels.
            if (value.EndsWith("%", StringComparison.Ordinal) || value.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                return $"'{EscapeJs(value)}'";

            // Fallback: treat as a CSS size string (e.g. calc expression).
            return $"'{EscapeJs(value)}'";
        }
    }
}
