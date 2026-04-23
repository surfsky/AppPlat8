using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// 提示容器。输出 el-tooltip。
    /// </summary>
    [HtmlTargetElement("EleTooltip")]
    public class EleTooltipTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("Content")]
        public string Content { get; set; }

        [HtmlAttributeName("Placement")]
        public string Placement { get; set; }

        [HtmlAttributeName("Trigger")]
        public string Trigger { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "el-tooltip";
            AddCommonAttributes(context, output);

            if (!string.IsNullOrWhiteSpace(Content))
                output.Attributes.SetAttribute("content", Content);
            if (!string.IsNullOrWhiteSpace(Placement))
                output.Attributes.SetAttribute("placement", Placement);
            if (!string.IsNullOrWhiteSpace(Trigger))
                output.Attributes.SetAttribute("trigger", Trigger);
        }
    }
}