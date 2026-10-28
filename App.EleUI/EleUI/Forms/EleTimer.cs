using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// 定时器开关组件
    /// </summary>
    [HtmlTargetElement("EleTimer")]
    public class EleTimer : EleControl
    {
        [HtmlAttributeName("Interval")]    public int Interval { get; set; } = 30;         // 刷新间隔秒数，默认 30
        [HtmlAttributeName("Triggle")]     public string Triggle { get; set; } = "Data";   // Post 到服务器的 handler 名称，默认 Data
        //[HtmlAttributeName("Text")]        public string Text { get; set; } = "自动刷新";   // 刷新开关文本，默认 "自动刷新"
        [HtmlAttributeName("TrueText")]    public string TrueText { get; set; } = "自动刷新";    // 开启状态文本
        [HtmlAttributeName("FalseText")]   public string FalseText { get; set; } = "关闭刷新";   // 关闭状态文本

        public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return Task.CompletedTask;

            output.TagName = "el-switch";
            // `el-switch` is a custom HTML element, so `/>` is not a self-closing
            // syntax in text/html. Emit an explicit closing tag to prevent the
            // browser from nesting following toolbar siblings inside this switch.
            output.TagMode = TagMode.StartTagAndEndTag;
            AddCommonAttributes(context, output);

            output.Attributes.SetAttribute("v-model", "autoRefreshEnabled");
            output.Attributes.SetAttribute("style", "margin-left:auto;");
            output.Attributes.SetAttribute("v-on:change", $"val => toggleAutoRefresh(val, {Interval}, '{EscapeJs(Triggle)}')");

            //output.Attributes.SetAttribute("active-text", this.Text);
           if (!string.IsNullOrWhiteSpace(TrueText) && !string.IsNullOrWhiteSpace(FalseText))
            {
                output.Attributes.SetAttribute("active-text", TrueText);
                output.Attributes.SetAttribute("inactive-text", FalseText);
                output.Attributes.SetAttribute("inline-prompt", "true");
            }

            return Task.CompletedTask;
        }
    }
}
