using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// 选择器组件
    /// </summary>
    [HtmlTargetElement("ElePicker")]
    public class ElePicker : EleFormControl
    {
        [HtmlAttributeName("PopupUrl")]      public string PopupUrl { get; set; }
        [HtmlAttributeName("Multi")]         public bool Multi { get; set; } = false;
        [HtmlAttributeName("TextFor")]       public ModelExpression TextFor { get; set; }
        [HtmlAttributeName("KeyMode")]       public string KeyMode { get; set; } = "Url";
        [HtmlAttributeName("Icon")]          public EleIcons Icon { get; set; } = EleIcons.Search;
        [HtmlAttributeName("Rows")]          public int Rows { get; set; } = 1;
        [HtmlAttributeName("Editable")]      public bool Editable { get; set; } = false;


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

            // Fallback to own field when no text field is provided.
            if (string.IsNullOrEmpty(textProp))
                textProp = propName;
            
            // Convert Text prop to camelCase for JS binding
            if (!string.IsNullOrEmpty(textProp) && textProp.Contains("."))
                textProp = textProp.Substring(textProp.LastIndexOf('.') + 1);
            textProp = ToCamelCase(textProp);

            //
            var popupUrl = (this.PopupUrl ?? string.Empty).Replace("'", "\\'");
            var title = (Label ?? "选择").Replace("'", "\\'");
            var multiStr = Multi.ToString().ToLower();
            var keyMode = (KeyMode ?? "Url").Replace("'", "\\'");
            var iconName = Icon == EleIcons.None ? EleIcons.Search : Icon;
            var rowCount = Rows > 0 ? Rows : 1;
            var isTextArea = rowCount > 1;
            var placeHolderText = ("请选择或输入" + (Label ?? "")).Replace("'", "\\'");

            // 禁用状态
            var enabledForPath = GetBindPath(EnabledFor);
            string vDisabledExpr;
            if (!string.IsNullOrWhiteSpace(enabledForPath))
            {
                var clientPath = ToClientFormPath(enabledForPath);
                vDisabledExpr = $"!({clientPath})";
            }
            else
            {
                vDisabledExpr = context.AllAttributes.ContainsName("Enabled")
                    ? (!Enabled).ToString().ToLower()
                    : "readOnly";
            }

            var target = ResolveControlTarget(context);
            var targetSafe = string.IsNullOrWhiteSpace(target) ? string.Empty : target.Replace("'", "\\'");
            var vVisibleExpr = string.IsNullOrWhiteSpace(target)
                ? "true"
                : $"(typeof resolveControlVisible === 'function' ? resolveControlVisible('{targetSafe}', true) : true)";
            var finalDisabledExpr = string.IsNullOrWhiteSpace(target)
                ? $"({vDisabledExpr})"
                : $"(typeof resolveControlDisabled === 'function' ? resolveControlDisabled('{targetSafe}', {vDisabledExpr}) : ({vDisabledExpr}))";

            //
            await RenderWrapper(output);

            //
            output.Attributes.SetAttribute("style", "width: 100%");
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "ele-picker-wrapper");
            output.Attributes.SetAttribute("v-show", vVisibleExpr);
            if (!string.IsNullOrWhiteSpace(target))
                output.Attributes.SetAttribute("data-ele-control-id", target);
            
            // Get form model name from context (default 'form')
            var formModel = context.Items.ContainsKey("EleFormModel") ? context.Items["EleFormModel"] as string : "form";
            string content;
            if (Editable)
            {
                var inputTypeHtml = isTextArea ? "type=\"textarea\"" : "";
                var rowHtml = isTextArea ? $":rows=\"{rowCount}\"" : "";
                if (isTextArea)
                {
                    content = $@"
                    <div class=""ele-picker-wrapper ele-picker-editable"" style=""width: 100%; position: relative;"">
                        <el-input v-model=""{formModel}.{textProp}"" {inputTypeHtml} {rowHtml} clearable placeholder=""{placeHolderText}"" :disabled=""{finalDisabledExpr}""></el-input>
                        <span class=""ele-picker-icon-wrap"" :class=""{{ 'cursor-pointer': !({finalDisabledExpr}), 'cursor-not-allowed': ({finalDisabledExpr}) }}"" style=""position:absolute;top:8px;right:10px;z-index:2;pointer-events:auto;background:rgba(255,255,255,0.92);border-radius:4px;padding:1px 3px;"" @click.stop=""!({finalDisabledExpr}) && openPicker('{propName}', '{textProp}', '{popupUrl}', {multiStr}, '{title}', '{keyMode}')"" title=""打开选择窗口"">
                            <el-icon><component :is=""({finalDisabledExpr}) ? 'Lock' : '{iconName}'""></component></el-icon>
                        </span>
                    </div>
                    ";
                }
                else
                {
                    content = $@"
                    <div class=""ele-picker-wrapper ele-picker-editable"" style=""width: 100%;"">
                        <el-input v-model=""{formModel}.{textProp}"" {inputTypeHtml} {rowHtml} clearable placeholder=""{placeHolderText}"" :disabled=""{finalDisabledExpr}"">
                            <template #suffix>
                                <span class=""ele-picker-icon-wrap"" :class=""{{ 'cursor-pointer': !({finalDisabledExpr}), 'cursor-not-allowed': ({finalDisabledExpr}) }}"" style=""pointer-events:auto;"" @click.stop=""!({finalDisabledExpr}) && openPicker('{propName}', '{textProp}', '{popupUrl}', {multiStr}, '{title}', '{keyMode}')"" title=""打开选择窗口"">
                                    <el-icon><component :is=""({finalDisabledExpr}) ? 'Lock' : '{iconName}'""></component></el-icon>
                                </span>
                            </template>
                        </el-input>
                    </div>
                    ";
                }
            }
            else
            {
                content = $@"
                <div class=""el-input el-input--suffix"" :class=""{{ 'cursor-pointer': !({finalDisabledExpr}), 'is-disabled': ({finalDisabledExpr}) }}"" style=""width: 100%;"" @click=""!({finalDisabledExpr}) && openPicker('{propName}', '{textProp}', '{popupUrl}', {multiStr}, '{title}', '{keyMode}')"">
                    <div class=""el-input__wrapper"" style=""width: 100%;"">
                        <div class=""flex flex-wrap gap-1 items-center w-full py-1"" style=""min-height: 30px;"">
                            <template v-if=""{formModel}.{textProp}"">
                                <el-tag type=""info"" class=""max-w-full overflow-hidden text-ellipsis whitespace-nowrap"" disable-transitions v-if=""!({finalDisabledExpr})"" closable @close.stop=""clearPicker('{propName}', '{textProp}')"">
                                    {{{{ {formModel}.{textProp} }}}}
                                </el-tag>
                                <el-tag type=""info"" class=""max-w-full overflow-hidden text-ellipsis whitespace-nowrap"" disable-transitions v-else>
                                    {{{{ {formModel}.{textProp} }}}}
                                </el-tag>
                            </template>
                            <span v-else class=""text-gray-400 text-sm"">请选择{Label}</span>
                        </div>
                        <span class=""el-input__suffix"">
                            <span class=""el-input__suffix-inner"" :class=""{{ 'cursor-pointer': !({finalDisabledExpr}), 'cursor-not-allowed': ({finalDisabledExpr}) }}"" style=""pointer-events:auto;"" title=""打开选择窗口"">
                                <el-icon><component :is=""({finalDisabledExpr}) ? 'Lock' : '{iconName}'""></component></el-icon>
                            </span>
                        </span>
                    </div>
                </div>
                ";
            }
            
            output.Content.SetHtmlContent(content);
        }
    }
}
