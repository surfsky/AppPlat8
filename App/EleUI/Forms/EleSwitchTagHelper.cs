using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleSwitch")]
    public class EleSwitchTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Checked")]
        public bool? Checked { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "el-switch";
            AddCommonAttributes(context, output);

            output.Attributes.SetAttribute(":active-value", "true");
            output.Attributes.SetAttribute(":inactive-value", "false");

            // In table/list filter context, switches should default to false even before user interaction.
            // The app builder reads this marker and initializes filters.{field} accordingly.
            var isEleForm = context.Items.ContainsKey("IsEleForm");
            if (!isEleForm)
            {
                if (!output.Attributes.ContainsName("data-filter-default"))
                    output.Attributes.SetAttribute("data-filter-default", (Checked ?? false).ToString().ToLowerInvariant());

                if (!output.Attributes.ContainsName("data-filter-model"))
                    output.Attributes.SetAttribute("data-filter-model", GetPropName());
            }

            await RenderWrapper(output);
        }
    }
}
