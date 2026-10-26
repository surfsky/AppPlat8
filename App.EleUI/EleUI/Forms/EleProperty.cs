using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>按钮位置</summary>
    public enum ElePropertyButtonPosition
    {
        Top,
        Bottom,
    }

    /// <summary>
    /// 属性编辑控件：将 JSON 对象编辑为 key/value 表格。
    /// </summary>
    [HtmlTargetElement("EleProperty")]
    public class EleProperty : EleFormControl
    {
        [HtmlAttributeName("MinHeight")]      public string MinHeight { get; set; } = null;
        [HtmlAttributeName("ButtonPosition")] public ElePropertyButtonPosition ButtonPosition { get; set; } = ElePropertyButtonPosition.Top;
        [HtmlAttributeName("ButtonText")]     public string ButtonText { get; set; } = "";
        [HtmlAttributeName("Editable")]       public bool Editable { get; set; } = true;
        [HtmlAttributeName("AddText")]        public string AddText { get; set; } = null;
        [HtmlAttributeName("FieldText")]      public string FieldText { get; set; } = null;
        [HtmlAttributeName("ValueText")]      public string ValueText { get; set; } = null;
        [HtmlAttributeName("ActionText")]     public string ActionText { get; set; } = null;
        [HtmlAttributeName("EmptyText")]      public string EmptyText { get; set; } = null;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "div";
            output.Attributes.SetAttribute("style", "width:100%;");

            var propName = GetPropName();
            var formModel = context.Items.ContainsKey("EleFormModel") ? context.Items["EleFormModel"] as string : "form";
            var addText = AddText ?? Texts.Current.AddAttribute;
            var fieldText = FieldText ?? Texts.Current.Field_;
            var valueText = ValueText ?? Texts.Current.Value_;
            var actionText = ActionText ?? Texts.Current.Operation;
            var emptyText = EmptyText ?? Texts.Current.NoAttribute;

            var enabledForPath = GetBindPath(EnabledFor);
            string baseDisabledExpr;
            if (!Editable)
            {
                baseDisabledExpr = "true";
            }
            else if (!string.IsNullOrWhiteSpace(enabledForPath))
            {
                var clientPath = ToClientFormPath(enabledForPath);
                baseDisabledExpr = $"!({clientPath})";
            }
            else
            {
                baseDisabledExpr = context.AllAttributes.ContainsName("Enabled")
                    ? (!Enabled).ToString().ToLower()
                    : (context.Items.ContainsKey("IsEleForm") ? "readOnly" : "false");
            }

            var target = ResolveControlTarget(context);
            var targetSafe = string.IsNullOrWhiteSpace(target) ? string.Empty : target.Replace("'", "\\'");
            var vVisibleExpr = string.IsNullOrWhiteSpace(target)
                ? "true"
                : $"(typeof resolveControlVisible === 'function' ? resolveControlVisible('{targetSafe}', true) : true)";
            var finalDisabledExpr = string.IsNullOrWhiteSpace(target)
                ? $"({baseDisabledExpr})"
                : $"(typeof resolveControlDisabled === 'function' ? resolveControlDisabled('{targetSafe}', {baseDisabledExpr}) : ({baseDisabledExpr}))";

            output.Attributes.SetAttribute("v-show", vVisibleExpr);
            if (!string.IsNullOrWhiteSpace(target))
                output.Attributes.SetAttribute("data-ele-control-id", target);

            // 计算滚动区域高度：用 Height/MinHeight 控制，参照 EleList
            // 默认 min-height 120px（约3行）；若设置了 Height 但未设 MinHeight，取 40px 使 max-height 生效
            var minHeight = !string.IsNullOrWhiteSpace(MinHeight)
                ? MinHeight.Trim()
                : (string.IsNullOrWhiteSpace(Height) ? "120px" : "40px");
            var maxHeight = string.IsNullOrWhiteSpace(Height) ? null : Height.Trim();
            var scrollStyle = maxHeight == null
                ? $"overflow:auto; min-height:{minHeight};"
                : $"overflow:auto; min-height:{minHeight}; max-height:{maxHeight};";

            // 新增按钮的 HTML：根据 ButtonPosition 决定放在表格上方或下方
            var addButton = $@"
    <div class=""{(ButtonPosition == ElePropertyButtonPosition.Top ? "mb-2" : "mt-2")} flex justify-end"" v-if=""!({finalDisabledExpr})"">
        <el-button size=""normal"" type=""primary"" plain title=""{addText}"" @click=""addPropertyRow('{propName}')"">
            <el-icon><Plus></Plus></el-icon>
            {ButtonText}
        </el-button>
    </div>";
            var topButton = ButtonPosition == ElePropertyButtonPosition.Top ? addButton : "";
            var bottomButton = ButtonPosition == ElePropertyButtonPosition.Bottom ? addButton : "";

            var content = $@"
<div class=""ele-property-wrapper"" style=""width:100%;"">{topButton}
    <div class=""border border-slate-200 rounded-md overflow-hidden"">
        <div style=""{scrollStyle}"">
            <table class=""w-full border-collapse"">
                <thead class=""sticky top-0 z-10"">
                    <tr class=""bg-slate-50 text-slate-600 text-xs"">
                        <th class=""border border-slate-200 px-2 py-2 text-left font-medium"" style=""width:38%;"">{fieldText}</th>
                        <th class=""border border-slate-200 px-2 py-2 text-left font-medium"">{valueText}</th>
                        <th class=""border border-slate-200 px-2 py-2 text-center font-medium"" style=""width:76px;"">{actionText}</th>
                    </tr>
                </thead>
                <tbody>
                    <tr v-if=""getPropertyRows('{propName}').length === 0"">
                        <td colspan=""3"" class=""border border-slate-200 px-2 py-3 text-xs text-slate-400 text-center"">{emptyText}</td>
                    </tr>
                    <tr v-for=""(row, idx) in getPropertyRows('{propName}')"" :key=""idx"">
                        <td class=""border border-slate-200 px-1 py-1"">
                            <el-input size=""small"" class=""ele-property-input"" style=""--el-input-border-color: transparent; --el-input-hover-border-color: transparent; --el-input-focus-border-color: transparent;"" v-model=""row.key"" placeholder=""字段名"" :disabled=""{finalDisabledExpr}"" @change=""syncPropertyJson('{propName}')""></el-input>
                        </td>
                        <td class=""border border-slate-200 px-1 py-1"">
                            <el-input size=""small"" class=""ele-property-input"" style=""--el-input-border-color: transparent; --el-input-hover-border-color: transparent; --el-input-focus-border-color: transparent;"" v-model=""row.value"" placeholder=""字段值"" :disabled=""{finalDisabledExpr}"" @change=""syncPropertyJson('{propName}')""></el-input>
                        </td>
                        <td class=""border border-slate-200 px-1 py-1 text-center"">
                            <el-button size=""small"" type=""danger"" text title=""删除"" :disabled=""{finalDisabledExpr}"" @click=""removePropertyRow('{propName}', idx)"">
                                <el-icon><Delete></Delete></el-icon>
                            </el-button>
                        </td>
                    </tr>
                </tbody>
            </table>
        </div>
    </div>{bottomButton}
    <input type=""hidden"" v-model=""{formModel}.{propName}"" />
</div>";

            output.Content.SetHtmlContent(content);
            await RenderWrapper(output);
        }
    }
}