using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleHidden")]
    public class EleHiddenTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("For")]
        public ModelExpression For { get; set; }

        [HtmlAttributeName("Prop")]
        public string Prop { get; set; }

        [HtmlAttributeName("Value")]
        public string Value { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "input";
            output.TagMode = TagMode.SelfClosing;
            output.Attributes.SetAttribute("type", "hidden");

            // Keep base v-if/v-show handling if explicitly set on tag.
            AddCommonAttributes(context, output);

            var vModel = GetVModel(context);
            if (!string.IsNullOrWhiteSpace(vModel))
            {
                output.Attributes.SetAttribute("v-model", vModel);
            }
            else if (!string.IsNullOrWhiteSpace(Value))
            {
                output.Attributes.SetAttribute(":value", Value);
            }

            await Task.CompletedTask;
        }

        private string GetVModel(TagHelperContext context)
        {
            if (!string.IsNullOrEmpty(VModel)) return VModel;
            if (!string.IsNullOrEmpty(Prop)) return Prop;
            if (For == null || string.IsNullOrEmpty(For.Name)) return null;

            var name = For.Name; // e.g. Item.Id
            var propName = name;
            if (propName.Contains('.'))
                propName = propName.Substring(propName.LastIndexOf('.') + 1);

            var camelName = ToCamelCase(propName);

            var isEleForm = context.Items.ContainsKey("IsEleForm");
            if (isEleForm)
            {
                var formModel = context.Items.ContainsKey("EleFormModel") ? context.Items["EleFormModel"] as string : "form";
                return $"{formModel}.{camelName}";
            }

            return $"filters.{camelName}";
        }

        private static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0])) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }
    }
}
