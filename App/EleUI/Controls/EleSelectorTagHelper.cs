using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// 选择器组件
    /// </summary>
    [HtmlTargetElement("EleSelector")]
    public class EleSelectorTagHelper : EleFormControlTagHelper
    {
        
        [HtmlAttributeName("PopupUrl")]
        public string PopupUrl { get; set; }

        [HtmlAttributeName("Multi")]
        public bool Multi { get; set; } = false;

        [HtmlAttributeName("TextFor")]
        public ModelExpression TextFor { get; set; }

        [HtmlAttributeName("Icon")]
        public EleIconName Icon { get; set; } = EleIconName.Search;


        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            // We render a wrapper that looks like a select but opens a popup
            output.TagName = "div";
            
            // Build the VModel for Id
            var vModel = GetVModel(context);
            var propName = GetPropName(); // e.g. chargeUserId
            var textProp = TextFor?.Name;

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
                textProp = textProp.Substring(textProp.LastIndexOf('.') + 1);
            textProp = ToCamelCase(textProp);

            //
            var popupUrl = this.PopupUrl;
            var title = Label ?? "选择";
            var multiStr = Multi.ToString().ToLower();
            var iconName = Icon == EleIconName.None ? EleIconName.Search : Icon;

            // 禁用状态
            var disabledForPath = GetBindPath(DisabledFor);
            string vDisabledExpr;
            if (!string.IsNullOrWhiteSpace(disabledForPath))
            {
                var clientPath = ToClientFormPath(disabledForPath);
                var modelType = Nullable.GetUnderlyingType(DisabledFor.ModelExplorer.ModelType) ?? DisabledFor.ModelExplorer.ModelType;
                vDisabledExpr = modelType == typeof(bool) ? clientPath : $"!({clientPath})";
            }
            else
            {
                vDisabledExpr = Disabled.HasValue ? Disabled.Value.ToString().ToLower() : "readOnly";
            }

            //
            await RenderWrapper(output);

            //
            output.Attributes.SetAttribute("style", "width: 100%");
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "ele-selector-wrapper");
            
            // Get form model name from context (default 'form')
            var formModel = context.Items.ContainsKey("EleFormModel") ? context.Items["EleFormModel"] as string : "form";
            var content = $@"
            <div class=""el-input el-input--suffix"" :class=""{{ 'cursor-pointer': !({vDisabledExpr}), 'is-disabled': ({vDisabledExpr}) }}"" style=""width: 100%;"" @click=""!({vDisabledExpr}) && openSelector('{propName}', '{textProp}', '{popupUrl}', {multiStr}, '{title}')"">
                <div class=""el-input__wrapper"" style=""width: 100%;"">
                    <div class=""flex flex-wrap gap-1 items-center w-full py-1"" style=""min-height: 30px;"">
                        <template v-if=""{formModel}.{textProp}"">
                            <el-tag type=""info"" disable-transitions v-if=""!({vDisabledExpr})"" closable @close.stop=""clearSelector('{propName}', '{textProp}')"">
                                {{{{ {formModel}.{textProp} }}}}
                            </el-tag>
                            <el-tag type=""info"" disable-transitions v-else>
                                {{{{ {formModel}.{textProp} }}}}
                            </el-tag>
                        </template>
                        <span v-else class=""text-gray-400 text-sm"">请选择{Label}</span>
                    </div>
                    <span class=""el-input__suffix"">
                        <span class=""el-input__suffix-inner"">
                            <el-icon><component :is=""({vDisabledExpr}) ? 'Lock' : '{iconName}'""></component></el-icon>
                        </span>
                    </span>
                </div>
            </div>
            ";
            
            output.Content.SetHtmlContent(content);
        }
    }
}
