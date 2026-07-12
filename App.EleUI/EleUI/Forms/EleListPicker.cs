using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using App.Utils;

namespace App.EleUI
{
    /// <summary>抽屉式列表选择器</summary>
    [HtmlTargetElement("EleListPicker")]
    public class EleListPicker : EleFormControl
    {
        [HtmlAttributeName("Items")] public object Items { get; set; }
        [HtmlAttributeName("Multiple")] public bool Multiple { get; set; }
        [HtmlAttributeName("AllowCreate")] public bool AllowCreate { get; set; }
        [HtmlAttributeName("Filterable")] public bool Filterable { get; set; } = true;
        [HtmlAttributeName("KeyField")] public string KeyField { get; set; }
        [HtmlAttributeName("TextField")] public string TextField { get; set; }
        [HtmlAttributeName("Value")] public object Value { get; set; }
        [HtmlAttributeName("Clearable")] public new bool? Clearable { get; set; } = true;
        [HtmlAttributeName("CollapseTags")] public bool? CollapseTags { get; set; } = true;
        [HtmlAttributeName("DrawerTitle")] public string DrawerTitle { get; set; }
        [HtmlAttributeName("DrawerSize")] public string DrawerSize { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            Placeholder = string.IsNullOrWhiteSpace(Placeholder) ? "请选择" : Placeholder;

            var propName = GetPropName();
            var target = ResolveControlTarget(context) ?? $"field:{propName}";
            var targetSafe = EscapeJs(target);
            var modelKeySafe = EscapeJs(propName);
            var onChangeName = EscapeJs(OnChange ?? string.Empty);
            var labelSafe = EscapeJs(Label ?? "选择");
            var titleSafe = EscapeJs(string.IsNullOrWhiteSpace(DrawerTitle) ? $"选择{Label}" : DrawerTitle);
            var sizeSafe = EscapeJs(DrawerSize ?? string.Empty);
            var collapseTags = CollapseTags ?? true;

            var options = new List<SelectListItem>();
            if (For != null)
                options = AppendFromEnum(options);
            if (Items != null)
                options.AddRange(ParseItems());
            if (Items == null && IsBooleanSelectTarget())
                options = AppendFromBool(options);

            var defaultOptionsJson = BuildDefaultOptionsJson(options);

            var vModel = GetVModel(context);
            if (Value != null && !string.IsNullOrWhiteSpace(vModel))
            {
                var defaultRaw = GetDefaultRaw(Value);
                var defaultExpr = GetDefaultValueExpression(Value);

                if (!context.Items.ContainsKey("IsEleForm") && vModel.StartsWith("filters.", StringComparison.Ordinal))
                {
                    output.Attributes.SetAttribute("data-filter-default", defaultRaw);
                    if (!string.IsNullOrWhiteSpace(propName))
                        output.Attributes.SetAttribute("data-filter-model", propName);
                }

                output.Attributes.SetAttribute(":data-default-value", defaultExpr);
            }

            output.TagName = "div";
            output.Attributes.SetAttribute("style", "width: 100%;");
            output.Attributes.SetAttribute("class", "ele-list-picker-wrapper");
            output.Attributes.SetAttribute("data-select-model", propName);
            output.Attributes.SetAttribute("data-select-target", target);
            output.Attributes.SetAttribute("data-static-options", defaultOptionsJson);
            output.Attributes.SetAttribute("data-ele-control-id", target);
            output.Attributes.SetAttribute("data-ele-field", propName);

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

            var visibleExpr = $"(typeof resolveControlVisible === 'function' ? resolveControlVisible('{targetSafe}', true) : true)";
            var disabledExpr = $"(typeof resolveControlDisabled === 'function' ? resolveControlDisabled('{targetSafe}', {baseDisabledExpr}) : ({baseDisabledExpr}))";
            var optionsExpr = "[]";
            var viewExpr = $"getListPickerView('{modelKeySafe}', '{targetSafe}', {optionsExpr}, {(Multiple ? "true" : "false")}, {(collapseTags ? "true" : "false")})";
            var openExpr = $@"openListPicker({{
                modelKey: '{modelKeySafe}',
                label: '{labelSafe}',
                title: '{titleSafe}',
                target: '{targetSafe}',
                multiple: {(Multiple ? "true" : "false")},
                allowCreate: {(AllowCreate ? "true" : "false")},
                filterable: {(Filterable ? "true" : "false")},
                collapseTags: {(collapseTags ? "true" : "false")},
                placeholder: '请输入关键字过滤',
                size: '{sizeSafe}',
                fallbackOptions: {optionsExpr},
                onChange: '{onChangeName}'
            }})";

            await RenderWrapper(output);
            output.Attributes.SetAttribute("v-show", visibleExpr);

            var content = $@"
<div class=""el-select w-full"" :class=""{{ 'is-disabled': ({disabledExpr}) }}"" @click=""!({disabledExpr}) && {openExpr}"">
    <div class=""el-select__wrapper cursor-pointer"" :class=""{{ 'is-disabled': ({disabledExpr}) }}"">
        <div class=""el-select__selection"">
            <template v-if=""{viewExpr}.labels.length"">
                <template v-if=""{(Multiple ? "true" : "false")}"">
                    <div class=""flex flex-wrap gap-1 items-center"">
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
                    </div>
                </template>
                <span v-else class=""el-select__selected-item truncate"">{{{{ {viewExpr}.text }}}}</span>
            </template>
            <span v-else class=""el-select__placeholder is-transparent"">{Placeholder}</span>
        </div>
        <div class=""el-select__suffix flex items-center gap-1"">
            <el-icon
                v-if=""{((Clearable ?? true) ? "true" : "false")} && {viewExpr}.labels.length > 0 && !({disabledExpr})""
                class=""cursor-pointer text-slate-400 hover:text-slate-600 mr-1""
                @click.stop=""clearListPicker('{modelKeySafe}', {(Multiple ? "true" : "false")}, '{onChangeName}')"">
                <CircleClose />
            </el-icon>
            <el-icon class=""el-select__caret pointer-events-none shrink-0 text-slate-400 text-[14px]""><ArrowDown /></el-icon>
        </div>
    </div>
</div>";

            output.Content.SetHtmlContent(content);
        }

        protected override string BuildOnChangePayloadExpression(TagHelperContext context, string valueExpression, string eventName = "change")
        {
            var basePayload = base.BuildOnChangePayloadExpression(context, valueExpression, eventName);
            return basePayload.TrimEnd('}') + $", controlType: 'EleListPicker', multiple: {(Multiple ? "true" : "false")} }}";
        }

        private bool IsBooleanSelectTarget()
        {
            var type = For?.ModelExplorer?.ModelType;
            if (type == null) return false;
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(bool);
        }

        /// <summary>根据For属性生成选项</summary>
        private List<SelectListItem> AppendFromEnum(List<SelectListItem> list)
        {
            var type = For.ModelExplorer.ModelType;
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                type = type.GetGenericArguments()[0];
                type = Nullable.GetUnderlyingType(type) ?? type;
            }
            else if (type.IsArray)
            {
                type = type.GetElementType();
                type = Nullable.GetUnderlyingType(type) ?? type;
            }

            if (!type.IsEnum)
                return list;

            foreach (var value in Enum.GetValues(type))
            {
                var info = EnumHelper.GetEnumInfo(value);
                list.Add(new SelectListItem(info.Title, ((int)info.Value).ToString()));
            }
            return list;
        }

        private List<SelectListItem> AppendFromBool(List<SelectListItem> list)
        {
            list.Add(new SelectListItem("是", "true"));
            list.Add(new SelectListItem("否", "false"));
            return list;
        }

        private string BuildDefaultOptionsJson(List<SelectListItem> list)
        {
            var targetType = GetSelectTargetType();
            var defaults = list.Select(i => new
            {
                label = i.Text,
                value = ConvertToTargetType(i.Value, targetType)
            }).ToList();
            return JsonSerializer.Serialize(defaults);
        }

        private Type GetSelectTargetType()
        {
            var targetType = For?.ModelExplorer.ModelType;
            if (targetType == null)
                return typeof(string);

            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                targetType = targetType.GetGenericArguments()[0];
                targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            }
            else if (targetType.IsArray)
            {
                targetType = targetType.GetElementType();
                targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            }

            return targetType;
        }

        private object ConvertToTargetType(string raw, Type targetType)
        {
            if (targetType != null && targetType.IsEnum)
            {
                if (int.TryParse(raw, out var enumValue))
                    return enumValue;
                if (Enum.TryParse(targetType, raw, true, out var enumObj))
                    return Convert.ToInt32(enumObj, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(bool) && bool.TryParse(raw, out var boolValue))
                return boolValue;

            if ((targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short)) && long.TryParse(raw, out var longValue))
            {
                if (targetType == typeof(int)) return (int)longValue;
                if (targetType == typeof(short)) return (short)longValue;
                return longValue;
            }

            if ((targetType == typeof(double) || targetType == typeof(decimal) || targetType == typeof(float)) && decimal.TryParse(raw, out var decimalValue))
            {
                if (targetType == typeof(double)) return (double)decimalValue;
                if (targetType == typeof(float)) return (float)decimalValue;
                return decimalValue;
            }

            return raw;
        }

        private static string GetDefaultRaw(object value)
        {
            if (value == null)
                return string.Empty;

            if (value is string s1 && bool.TryParse(s1, out var sBool1))
                return sBool1 ? "true" : "false";

            if (value is bool b)
                return b ? "true" : "false";

            var t = value.GetType();
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t.IsEnum)
                return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            if (value is sbyte or byte or short or ushort or int or uint or long or ulong)
                return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            if (value is float or double or decimal)
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            return value.ToString();
        }

        private static string GetDefaultValueExpression(object value)
        {
            if (value == null)
                return "null";

            if (value is string s1 && bool.TryParse(s1, out var sBool1))
                return sBool1 ? "true" : "false";

            if (value is bool b)
                return b ? "true" : "false";

            var t = value.GetType();
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t.IsEnum)
                return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            if (value is sbyte or byte or short or ushort or int or uint or long or ulong)
                return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            if (value is float or double or decimal)
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            return JsonSerializer.Serialize(value.ToString());
        }

        /// <summary>解析Items属性，返回SelectListItem列表</summary>
        private List<SelectListItem> ParseItems()
        {
            var list = new List<SelectListItem>();
            if (Items is IEnumerable<SelectListItem> selectList)
            {
                list.AddRange(selectList);
            }
            else if (Items is string strItems)
            {
                strItems = strItems.Trim();
                if (strItems.StartsWith("[") && strItems.EndsWith("]"))
                {
                    try
                    {
                        var json = strItems.Replace("'", "\"");
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in doc.RootElement.EnumerateArray())
                            {
                                var val = item.ToString();
                                list.Add(new SelectListItem(val, val));
                            }
                        }
                    }
                    catch { }
                }
                else if (strItems.StartsWith("{") && strItems.EndsWith("}"))
                {
                    try
                    {
                        var json = strItems.Replace("'", "\"");
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var item in doc.RootElement.EnumerateObject())
                            {
                                list.Add(new SelectListItem(item.Name, item.Value.ToString()));
                            }
                        }
                    }
                    catch { }
                }
                else
                {
                    var arr = strItems.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var item in arr)
                    {
                        var val = item.Trim();
                        if (!string.IsNullOrEmpty(val))
                            list.Add(new SelectListItem(val, val));
                    }
                }
            }
            else if (Items is IDictionary dictionary)
            {
                foreach (DictionaryEntry item in dictionary)
                    list.Add(new SelectListItem(item.Key?.ToString(), item.Value?.ToString()));
            }
            else if (Items is IEnumerable<ListItem> eleList)
            {
                foreach (var item in eleList)
                    list.Add(new SelectListItem(item.Label, item.Value?.ToString() ?? string.Empty));
            }
            else if (Items is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    if (item is string || item.GetType().IsValueType)
                    {
                        var primitiveValue = item.ToString();
                        list.Add(new SelectListItem(primitiveValue, primitiveValue));
                        continue;
                    }

                    var textObj = GetObjectFieldValue(item, TextField, new[] { "Name", "Text", "Label", "Title", "Value", "Id" });
                    var text = ToInvariantString(textObj);
                    var valueObj = GetObjectFieldValue(item, KeyField, new[] { "Id", "Value", "Key", "Code", "Name" });
                    var value = ToInvariantString(valueObj);
                    if (string.IsNullOrEmpty(value))
                        value = item.ToString();

                    list.Add(new SelectListItem(text ?? value, value));
                }
            }

            return list;
        }

        private object GetObjectFieldValue(object obj, string preferredField, string[] fallbackFields)
        {
            if (obj == null) return null;
            if (!string.IsNullOrWhiteSpace(preferredField))
            {
                var preferred = GetPropertyValue(obj, preferredField);
                if (preferred != null) return preferred;
            }

            foreach (var field in fallbackFields)
            {
                var value = GetPropertyValue(obj, field);
                if (value != null) return value;
            }

            return null;
        }

        private object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null || string.IsNullOrWhiteSpace(propertyName)) return null;
            var prop = obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));
            return prop?.GetValue(obj);
        }

        private string ToInvariantString(object value)
        {
            if (value == null) return null;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
