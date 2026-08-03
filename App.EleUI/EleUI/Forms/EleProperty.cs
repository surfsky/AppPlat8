using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// 属性编辑控件：将 JSON 对象编辑为 key/value 表格。
    /// </summary>
    [HtmlTargetElement("EleProperty")]
    public class EleProperty : EleFormControl
    {
        [HtmlAttributeName("Rows")] public int Rows { get; set; } = 4;
        [HtmlAttributeName("Editable")] public bool Editable { get; set; } = true;
        [HtmlAttributeName("AddText")] public string AddText { get; set; } = "新增属性";
        [HtmlAttributeName("FieldText")] public string FieldText { get; set; } = "字段";
        [HtmlAttributeName("ValueText")] public string ValueText { get; set; } = "值";
        [HtmlAttributeName("ActionText")] public string ActionText { get; set; } = "操作";
        [HtmlAttributeName("EmptyText")] public string EmptyText { get; set; } = "暂无属性";

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "div";
            output.Attributes.SetAttribute("style", "width:100%;");

            var propName = GetPropName();
            var formModel = context.Items.ContainsKey("EleFormModel") ? context.Items["EleFormModel"] as string : "form";

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
                    : "readOnly";
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

            var minRows = Rows > 0 ? Rows : 4;
            var minHeight = minRows * 38 + 46;

            var content = $@"
<div class=""ele-property-wrapper"" style=""width:100%;"">
    <div class=""mb-2 flex justify-end"" v-if=""!({finalDisabledExpr})"">
        <el-button size=""small"" type=""primary"" plain title=""{AddText}"" @click=""addPropertyRow('{propName}')"">
            <el-icon><Plus></Plus></el-icon>
        </el-button>
    </div>
    <div class=""border border-slate-200 rounded-md overflow-hidden"">
        <div style=""overflow:auto; min-height:{minHeight}px;"">
            <table class=""w-full border-collapse"">
                <thead>
                    <tr class=""bg-slate-50 text-slate-600 text-xs"">
                        <th class=""border border-slate-200 px-2 py-2 text-left font-medium"" style=""width:38%;"">{FieldText}</th>
                        <th class=""border border-slate-200 px-2 py-2 text-left font-medium"">{ValueText}</th>
                        <th class=""border border-slate-200 px-2 py-2 text-center font-medium"" style=""width:76px;"">{ActionText}</th>
                    </tr>
                </thead>
                <tbody>
                    <tr v-if=""getPropertyRows('{propName}').length === 0"">
                        <td colspan=""3"" class=""border border-slate-200 px-2 py-3 text-xs text-slate-400 text-center"">{EmptyText}</td>
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
    </div>
    <input type=""hidden"" v-model=""{formModel}.{propName}"" />
</div>";

            output.Content.SetHtmlContent(content);
            await RenderWrapper(output);
        }
    }
}