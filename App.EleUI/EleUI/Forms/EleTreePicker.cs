using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Collections;
using System.Linq;
using App.Utils;

namespace App.EleUI
{
    /// <summary>
    /// 树选择抽屉组件，使用根级 Drawer 展示树数据供选择。
    /// </summary>
    [HtmlTargetElement("EleTreePicker")]
    public class EleTreePicker : EleFormControl
    {
        [HtmlAttributeName("Api")]           public string Api { get; set; }
        [HtmlAttributeName("Items")]         public object Items { get; set; }
        [HtmlAttributeName("IdField")]       public string IdField { get; set; }
        [HtmlAttributeName("ValueField")]    public string ValueField { get; set; }
        [HtmlAttributeName("NameField")]     public string NameField { get; set; }
        [HtmlAttributeName("ChildrenField")] public string ChildrenField { get; set; }
        [HtmlAttributeName("CheckStrictly")] public bool CheckStrictly { get; set; } = true;
        [HtmlAttributeName("Multiple")]      public bool Multiple { get; set; } = false;
        [HtmlAttributeName("CollapseTags")]  public bool? CollapseTags { get; set; } = false;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            Placeholder = string.IsNullOrEmpty(Placeholder) ? $"请选择{Label}" : Placeholder;

            var propName = GetPropName();
            var target = ResolveControlTarget(context) ?? $"field:{propName}";
            var idField = NormalizeFieldForVue(IdField ?? ValueField ?? "id");
            var nameField = NormalizeFieldForVue(NameField ?? "name");
            var childrenField = NormalizeFieldForVue(ChildrenField ?? "children");
            var collapseTags = CollapseTags ?? false;

            var fallbackExpr = "[]";
            if (Items is IEnumerable enumerable && Items is not string)
            {
                var (sourceIdField, sourceNameField, sourceChildrenField) = ResolveItemsFieldNames(enumerable);
                idField = sourceIdField;
                nameField = sourceNameField;
                childrenField = sourceChildrenField;
                fallbackExpr = Items.ToJson();
                output.Attributes.SetAttribute("data-static-items", fallbackExpr);
            }

            if (!string.IsNullOrWhiteSpace(Api))
            {
                output.Attributes.SetAttribute("data-source", Api);
                output.Attributes.SetAttribute("data-key", propName);
                fallbackExpr = $"options['{EscapeJs(propName)}']";
            }

            output.TagName = "div";
            output.Attributes.SetAttribute("style", "width: 100%;");
            output.Attributes.SetAttribute("class", "ele-tree-picker-wrapper");
            output.Attributes.SetAttribute("data-tree-model", propName);
            output.Attributes.SetAttribute("data-tree-id-field", idField);
            output.Attributes.SetAttribute("data-tree-children-field", childrenField);

            var enabledForPath = GetBindPath(EnabledFor);
            string baseDisabledExpr;
            if (!string.IsNullOrWhiteSpace(enabledForPath))
            {
                var clientPath = ToClientFormPath(enabledForPath);
                baseDisabledExpr = $"!({clientPath})";
            }
            else if (context.AllAttributes.ContainsName("Enabled"))
            {
                baseDisabledExpr = (!Enabled).ToString().ToLower();
            }
            else
            {
                baseDisabledExpr = "readOnly";
            }

            var targetSafe = EscapeJs(target);
            var visibleExpr = $"(typeof resolveControlVisible === 'function' ? resolveControlVisible('{targetSafe}', true) : true)";
            var disabledExpr = $"(typeof resolveControlDisabled === 'function' ? resolveControlDisabled('{targetSafe}', {baseDisabledExpr}) : ({baseDisabledExpr}))";
            var onChangeName = EscapeJs(OnChange ?? string.Empty);
            var labelSafe = EscapeJs(Label ?? "选择");
            var apiSafe = EscapeJs(Api ?? string.Empty);
            var viewExpr = $"getTreePickerView('{EscapeJs(propName)}', '{targetSafe}', {fallbackExpr}, '{EscapeJs(idField)}', '{EscapeJs(nameField)}', '{EscapeJs(childrenField)}', {(Multiple ? "true" : "false")}, {(collapseTags ? "true" : "false")})";
            var openExpr = $@"openTreePicker({{
                modelKey: '{EscapeJs(propName)}',
                label: '{labelSafe}',
                title: '选择{labelSafe}',
                target: '{targetSafe}',
                source: '{apiSafe}',
                idField: '{EscapeJs(idField)}',
                nameField: '{EscapeJs(nameField)}',
                childrenField: '{EscapeJs(childrenField)}',
                multiple: {(Multiple ? "true" : "false")},
                checkStrictly: {(CheckStrictly ? "true" : "false")},
                collapseTags: {(collapseTags ? "true" : "false")},
                placeholder: '输入关键字过滤',
                fallbackData: {fallbackExpr},
                onChange: '{onChangeName}'
            }})";

            await RenderWrapper(output);

            output.Attributes.SetAttribute("v-show", visibleExpr);
            output.Attributes.SetAttribute("data-ele-control-id", target);
            output.Attributes.SetAttribute("data-ele-field", propName);

            var content = $@"
<div class=""el-input el-input--suffix"" :class=""{{ 'cursor-pointer': !({disabledExpr}), 'is-disabled': ({disabledExpr}) }}"" style=""width: 100%;"" @click=""!({disabledExpr}) && {openExpr}"">
    <div class=""el-input__wrapper"" style=""width: 100%;"">
        <div class=""flex flex-wrap gap-1 items-center w-full py-1"" style=""min-height: 32px;"">
            <template v-if=""{viewExpr}.labels.length"">
                <template v-if=""{(Multiple ? "true" : "false")}"">
                    <el-tag
                        v-for=""(txt, idx) in {viewExpr}.displayLabels""
                        :key=""idx""
                        type=""info""
                        disable-transitions
                        class=""max-w-full overflow-hidden text-ellipsis whitespace-nowrap""
                    >
                        {{{{ txt }}}}
                    </el-tag>
                    <el-tag v-if=""{viewExpr}.hiddenCount > 0"" type=""info"" disable-transitions>
                        +{{{{ {viewExpr}.hiddenCount }}}}
                    </el-tag>
                </template>
                <span v-else class=""w-full truncate text-sm text-slate-700"">{{{{ {viewExpr}.text }}}}</span>
            </template>
            <span v-else class=""text-gray-400 text-sm"">{Placeholder}</span>
        </div>
        <span class=""el-input__suffix"">
            <span class=""el-input__suffix-inner flex items-center gap-1"" style=""pointer-events:auto;"">
                <el-icon
                    v-if=""{(Clearable == false ? "false" : "true")} && {viewExpr}.labels.length > 0 && !({disabledExpr})""
                    class=""cursor-pointer text-slate-400 hover:text-slate-600""
                    @click.stop=""clearTreePicker('{EscapeJs(propName)}', {(Multiple ? "true" : "false")}, '{onChangeName}')""
                >
                    <Close />
                </el-icon>
                <el-icon :class=""{{ 'text-slate-400': ({disabledExpr}) }}"" title=""打开树选择抽屉""><Search /></el-icon>
            </span>
        </span>
    </div>
</div>";

            output.Content.SetHtmlContent(content);
        }

        /// <summary>推断静态树字段名。</summary>
        private (string idField, string nameField, string childrenField) ResolveItemsFieldNames(IEnumerable items)
        {
            var idField = IdField ?? ValueField;
            var nameField = NameField;
            var childrenField = ChildrenField ?? "children";

            var firstItem = items.Cast<object>().FirstOrDefault(x => x != null);
            if (firstItem == null)
            {
                return (
                    NormalizeFieldForVue(idField ?? "id"),
                    NormalizeFieldForVue(nameField ?? "name"),
                    NormalizeFieldForVue(childrenField)
                );
            }

            var props = firstItem.GetType().GetProperties().Select(p => p.Name).ToList();

            idField ??= PickField(props, "id", "value") ?? "id";
            nameField ??= PickField(props, "name", "label", "title") ?? "name";
            childrenField = string.IsNullOrEmpty(ChildrenField)
                ? (PickField(props, "children") ?? "children")
                : childrenField;

            idField = NormalizeFieldForSerializedJson(props, idField);
            nameField = NormalizeFieldForSerializedJson(props, nameField);
            childrenField = NormalizeFieldForSerializedJson(props, childrenField);

            return (idField, nameField, childrenField);
        }

        /// <summary>规范字段名到 camelCase。</summary>
        private string NormalizeFieldForVue(string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;
            return ToCamelCase(field);
        }

        /// <summary>根据序列化结果修正字段名。</summary>
        private string NormalizeFieldForSerializedJson(IEnumerable<string> props, string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;

            var match = props.FirstOrDefault(p => string.Equals(p, field, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match))
                return ToCamelCase(match);

            return field;
        }

        /// <summary>从候选名里挑选字段。</summary>
        private static string PickField(IEnumerable<string> props, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var match = props.FirstOrDefault(p => string.Equals(p, candidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match))
                    return match;
            }
            return null;
        }
    }
}
