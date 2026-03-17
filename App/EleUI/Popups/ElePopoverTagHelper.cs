using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// 气泡卡片容器。输出 el-popover。
    /// </summary>
    [HtmlTargetElement("ElePopover")]
    public class ElePopoverTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("Placement")]
        public string Placement { get; set; }

        [HtmlAttributeName("Title")]
        public string Title { get; set; }

        [HtmlAttributeName("Width")]
        public string PopoverWidth { get; set; }

        [HtmlAttributeName("Trigger")]
        public string Trigger { get; set; }

        [HtmlAttributeName("Content")]
        public string Content { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "el-popover";
            AddCommonAttributes(context, output);

            if (!string.IsNullOrWhiteSpace(Placement))
                output.Attributes.SetAttribute("placement", Placement);
            if (!string.IsNullOrWhiteSpace(Title))
                output.Attributes.SetAttribute("title", Title);
            if (!string.IsNullOrWhiteSpace(PopoverWidth))
                output.Attributes.SetAttribute("width", PopoverWidth);
            if (!string.IsNullOrWhiteSpace(Trigger))
                output.Attributes.SetAttribute("trigger", Trigger);
            if (!string.IsNullOrWhiteSpace(Content))
                output.Attributes.SetAttribute("content", Content);
        }
    }
}