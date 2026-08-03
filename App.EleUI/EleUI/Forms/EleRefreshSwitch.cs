using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleRefreshSwitch")]
    public class EleRefreshSwitch : EleControl
    {
        /// <summary>刷新间隔秒数，默认 30</summary>
        [HtmlAttributeName("Interval")]
        public int Interval { get; set; } = 30;

        /// <summary>Post 到服务器的 handler 名称，默认 Data</summary>
        [HtmlAttributeName("Triggle")]
        public string Triggle { get; set; } = "Data";

        public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return Task.CompletedTask;

            output.TagName = "el-switch";
            AddCommonAttributes(context, output);

            output.Attributes.SetAttribute("v-model", "autoRefreshEnabled");
            output.Attributes.SetAttribute("active-text", "定时刷新");
            output.Attributes.SetAttribute("style", "margin-left:auto;");
            output.Attributes.SetAttribute("v-on:change",
                $"val => toggleAutoRefresh(val, {Interval}, '{EscapeJs(Triggle)}')");

            return Task.CompletedTask;
        }
    }
}
