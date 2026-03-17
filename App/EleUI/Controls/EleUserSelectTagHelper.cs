using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleUserSelect")]
    public class EleUserSelectTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Target")]
        public string Target { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "ele-user-select";
            
            var vModel = GetVModel(context);
            if (!string.IsNullOrEmpty(vModel))
                output.Attributes.SetAttribute("v-model", vModel);
            else if (!string.IsNullOrEmpty(Prop))
                output.Attributes.SetAttribute("v-model", Prop);
            else
            {
                // Fallback: check if 'v-model' is already set in attributes (TagHelper attribute passing)
                // But TagHelper attributes are consumed by properties.
                // If the user wrote `v-model="..."`, it's not a property unless we define it.
                // Wait, custom attributes are in `output.Attributes`? No, context.AllAttributes?
                
                if (context.AllAttributes.ContainsName("v-model"))
                {
                    output.Attributes.SetAttribute("v-model", context.AllAttributes["v-model"].Value);
                }
            }
            
            if (!string.IsNullOrEmpty(Target))
            {
                // We want to bind it as a dynamic property usually, e.g. :target="xxx"
                // But TagHelper property is string.
                // If user wants binding, they should use BindTarget or we detect it?
                // Or we just output it as `:target` if it looks like a variable?
                // The error `TagHelper attributes must be well-formed` usually means Razor parser didn't like something.
                // `:target` is not a valid C# property name for TagHelper unless we use `[HtmlAttributeName(":target")]`? No.
                // The user wrote `:target="userSelectTarget"`. Razor treats `:` as part of attribute name if not bound.
                // But our `Target` property binds to `Target`.
                // So `:target` is a separate attribute.
                
                // If we want to support `:target`, we should not define `Target` property? 
                // Or we define `BindTarget`?
                
                // Let's assume Target property is for static value, and we output `target="..."`.
                // But the user wrote `:target`.
                
                output.Attributes.SetAttribute(":target", Target);
            }
            
            output.Attributes.SetAttribute("@selected", "handleUserSelected");
            
            // We assume the component is registered globally.
            output.TagMode = TagMode.StartTagAndEndTag;
        }
    }
}
