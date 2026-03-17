using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// 分隔面板布局方向
    /// </summary>
    public enum SplitPanelLayout
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
        [HtmlAttributeName("Layout")]
        public SplitPanelLayout Layout { get; set; } = SplitPanelLayout.Horizontal;

        /// <summary>Whether to enable lazy mode. Default: false</summary>
        [HtmlAttributeName("Lazy")]
        public bool Lazy { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "el-splitter";
            AddCommonAttributes(context, output);

            output.Attributes.SetAttribute("layout", Layout.ToString().ToLower());
            if (Lazy) output.Attributes.SetAttribute("lazy", "true");

            // Element Plus splitter needs explicit height often, handled by EleTagHelper.Height -> style
            if (string.IsNullOrEmpty(Height) && string.IsNullOrEmpty(output.Attributes["style"]?.Value?.ToString()))
            {
                 // Maybe default to something? Or let user specify.
                 // Usually splitters take full height of parent or explicit height.
                 // Let's leave it to user to specify Height="500px" or class="h-full"
            }
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

            if (!string.IsNullOrEmpty(Size)) output.Attributes.SetAttribute(":size", Size); // Should this be v-model:size? TagHelper doesn't easily support two-way binding syntax generation for child unless we use specific prop.
            // But usually just passing :size works for initial value. 
            // If user wants v-model, they can use VModel attribute from base class? 
            // No, base class VModel maps to `v-model`. 
            // Element Plus uses `v-model:size`. 
            // Let's stick to `:size` for now or check if VModel can be used.
            // If user passes `Size="20"`, it renders `:size="20"`.
            // If user wants `v-model:size="val"`, they can use manual attribute or we add `BindSize`.
            // Let's assume `Size` attribute value is a JS expression if it starts with numeric? 
            // Or just string. "200" -> size="200" (px). "30" -> size="30" (if int, might be %).
            // Element Plus docs: "string / number". 
            // Let's just pass it as attribute. If user wants binding, they use `v-bind:size`.
            // Wait, standard behavior for TagHelper:
            // If I set `Size="200"`, it outputs `size="200"`.
            // If I want binding, I usually need a property that outputs `:size`.
            // Let's just output `size` attribute for static values, and maybe `BindSize` for dynamic?
            // Actually, let's just use `output.Attributes.SetAttribute("size", Size)` if it's not null.
            
            // Correction: `el-splitter-panel` prop is `size`.
            if (!string.IsNullOrEmpty(Size)) output.Attributes.SetAttribute(":size", Size); 
            
            if (!string.IsNullOrEmpty(Min)) output.Attributes.SetAttribute(":min", Min);
            if (!string.IsNullOrEmpty(Max)) output.Attributes.SetAttribute(":max", Max);
            
            if (Resizable == false) output.Attributes.SetAttribute(":resizable", "false");
            if (Collapsible) output.Attributes.SetAttribute(":collapsible", "true");
        }
    }
}
