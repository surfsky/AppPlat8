using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

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

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            output.TagName = "el-splitter-panel";
            AddCommonAttributes(context, output);

            if (!string.IsNullOrEmpty(Size)) output.Attributes.SetAttribute(":size", Size); 
            if (!string.IsNullOrEmpty(Min))  output.Attributes.SetAttribute(":min", Min);
            if (!string.IsNullOrEmpty(Max))  output.Attributes.SetAttribute(":max", Max);
            if (Resizable == false)          output.Attributes.SetAttribute(":resizable", "false");
            if (Collapsible)                 output.Attributes.SetAttribute(":collapsible", "true");
        }
    }
}
