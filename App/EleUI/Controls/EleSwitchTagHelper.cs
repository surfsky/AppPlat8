using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleSwitch")]
    public class EleSwitchTagHelper : EleFormControlTagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "el-switch";
            AddCommonAttributes(context, output);
            await RenderWrapper(output);
        }
    }
}
