using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// 选择器组件
    /// </summary>
    [HtmlTargetElement("EleSelector")]
    public class EleSelectorTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Popup")]
        public string Popup { get; set; }

        [HtmlAttributeName("Multi")]
        public bool Multi { get; set; } = false;

        [HtmlAttributeName("Text")]
        public string Text { get; set; } // The property name for displaying text (e.g. Item.UserName)

        [HtmlAttributeName("Src")]
        public string Src { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            // We render a wrapper that looks like a select but opens a popup
            output.TagName = "div";
            
            // Build the VModel for Id
            var vModel = GetVModel(context);
            var propName = GetPropName(); // e.g. chargeUserId
            var textProp = Text;

            // If TextProp is not provided, try to guess from For (e.g. Item.ChargeUserId -> Item.ChargeUserName)
            if (string.IsNullOrEmpty(textProp) && For != null)
            {
                var name = For.Name;
                if (name.EndsWith("ID") || name.EndsWith("Id"))
                {
                    var baseName = name.Substring(0, name.Length - 2);
                    textProp = baseName + "Name"; // e.g. ChargeUserName
                }
            }
            
            // Convert Text prop to camelCase for JS binding
            if (!string.IsNullOrEmpty(textProp) && textProp.Contains("."))
            {
                textProp = textProp.Substring(textProp.LastIndexOf('.') + 1);
            }
            textProp = ToCamelCase(textProp);

            var popupUrl = !string.IsNullOrEmpty(Src) ? Src : Popup;
            var title = Label ?? "选择";
            var multiStr = Multi.ToString().ToLower();

            // We need to bind to `form.chargeUserId` and display `form.chargeUserName`.
            // The mixin will handle the popup and data return.
            
            // The HTML structure:
            /*
            <div class="el-input el-input--suffix group cursor-pointer" @click="openSelector(...)">
                <div class="el-input__wrapper">
                    <div class="flex flex-wrap gap-1 items-center w-full py-1">
                         <!-- Multi Mode -->
                         <template v-if="true">
                             <el-tag v-if="form.chargeUserName" type="info" closable @close.stop="clearSelector(...)">
                                {{ form.chargeUserName }}
                             </el-tag>
                         </template>
                         <!-- Placeholder -->
                         <span v-if="!form.chargeUserName" class="text-gray-400 text-sm">请选择{Label}</span>
                    </div>
                    <span class="el-input__suffix">
                        <el-icon><Search /></el-icon>
                    </span>
                </div>
            </div>
            */

            await RenderWrapper(output); // Renders el-col and el-form-item wrapper
            
            // Apply width style to the wrapper (ele-selector-wrapper)
            output.Attributes.SetAttribute("style", "width: 100%");

            // Now content inside el-form-item
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "ele-selector-wrapper");
            
            // Get form model name from context (default 'form')
            var formModel = context.Items.ContainsKey("EleFormModel") ? context.Items["EleFormModel"] as string : "form";

            var content = $@"
            <div class=""el-input el-input--suffix cursor-pointer"" style=""width: 100%;"" @click=""openSelector('{propName}', '{textProp}', '{popupUrl}', {multiStr}, '{title}')"">
                <div class=""el-input__wrapper"" style=""width: 100%;"">
                    <div class=""flex flex-wrap gap-1 items-center w-full py-1"" style=""min-height: 30px;"">
                        <template v-if=""{formModel}.{textProp}"">
                            <el-tag type=""info"" disable-transitions closable @close.stop=""clearSelector('{propName}', '{textProp}')"">
                                {{{{ {formModel}.{textProp} }}}}
                            </el-tag>
                        </template>
                        <span v-else class=""text-gray-400 text-sm"">请选择{Label}</span>
                    </div>
                    <span class=""el-input__suffix"">
                        <span class=""el-input__suffix-inner"">
                            <el-icon><Search /></el-icon>
                        </span>
                    </span>
                </div>
            </div>
            ";
            
            output.Content.SetHtmlContent(content);
        }
    }
}
