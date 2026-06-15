using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using App.Utils;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// el-tree组件，支持树状结构的显示和交互。
    /// </summary>
    [HtmlTargetElement("EleTree")]
    public class EleTree : EleControl
    {
        [HtmlAttributeName("Items")] public object Items { get; set; }
        [HtmlAttributeName("IdField")] public string IdField { get; set; }
        [HtmlAttributeName("NameField")] public string NameField { get; set; }
        [HtmlAttributeName("ChildrenField")] public string ChildrenField { get; set; }
        [HtmlAttributeName("DisabledField")] public string DisabledField { get; set; }
        [HtmlAttributeName("NodeKey")] public string NodeKey { get; set; }

        [HtmlAttributeName("ShowCheckbox")] public bool ShowCheckbox { get; set; }
        [HtmlAttributeName("CheckStrictly")] public bool CheckStrictly { get; set; }
        [HtmlAttributeName("DefaultExpandAll")] public bool DefaultExpandAll { get; set; }
        [HtmlAttributeName("Accordion")] public bool Accordion { get; set; }
        [HtmlAttributeName("HighlightCurrent")] public bool HighlightCurrent { get; set; }
        [HtmlAttributeName("Draggable")] public bool Draggable { get; set; }

        [HtmlAttributeName("ExpandOnClickNode")] public bool? ExpandOnClickNode { get; set; }
        [HtmlAttributeName("CheckOnClickNode")] public bool? CheckOnClickNode { get; set; }
        [HtmlAttributeName("RenderAfterExpand")] public bool? RenderAfterExpand { get; set; }
        [HtmlAttributeName("AutoExpandParent")] public bool? AutoExpandParent { get; set; }

        [HtmlAttributeName("DefaultExpandedKeys")] public object DefaultExpandedKeys { get; set; }
        [HtmlAttributeName("DefaultCheckedKeys")] public object DefaultCheckedKeys { get; set; }
        [HtmlAttributeName("CurrentNodeKey")] public object CurrentNodeKey { get; set; }

        [HtmlAttributeName("Indent")] public int? Indent { get; set; }
        [HtmlAttributeName("EmptyText")] public string EmptyText { get; set; }
        [HtmlAttributeName("FilterNodeMethod")] public string FilterNodeMethod { get; set; }

        [HtmlAttributeName("NodeClick")] public string NodeClick { get; set; }
        [HtmlAttributeName("CurrentChange")] public string CurrentChange { get; set; }
        [HtmlAttributeName("CheckChange")] public string CheckChange { get; set; }
        [HtmlAttributeName("OnNodeClick")] public string OnNodeClick { get; set; }
        [HtmlAttributeName("OnCheckChange")] public string OnCheckChange { get; set; }
        [HtmlAttributeName("Target")] public string Target { get; set; }
        [HtmlAttributeName("UrlTemplate")] public string UrlTemplate { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "el-tree";
            AddCommonAttributes(context, output);

            // Keep tree content inside its own box when a fixed height is set.
            if (!string.IsNullOrWhiteSpace(Height))
            {
                var style = output.Attributes["style"]?.Value?.ToString() ?? string.Empty;
                if (!style.Contains("overflow:", System.StringComparison.OrdinalIgnoreCase))
                    style += "overflow: auto;";
                if (!style.Contains("box-sizing:", System.StringComparison.OrdinalIgnoreCase))
                    style += "box-sizing: border-box;";
                output.Attributes.SetAttribute("style", style);
            }

            var childContent = await output.GetChildContentAsync();
            var slotContent = childContent.GetContent();

            var itemsEnumerable = Items as IEnumerable;
            var (idField, nameField, childrenField, disabledField) = ResolveItemsFieldNames(itemsEnumerable);
            var nodeKey = NormalizeFieldForVue(NodeKey ?? idField ?? "id");

            if (itemsEnumerable != null && Items is not string)
            {
                var jsonItems = Items.ToJson();
                output.Attributes.SetAttribute(":data", ToVueExpression(jsonItems));
            }
            else if (Items is string expression && !string.IsNullOrWhiteSpace(expression))
            {
                output.Attributes.SetAttribute(":data", ToVueExpression(expression.Trim()));
            }
            else
            {
                output.Attributes.SetAttribute(":data", ToVueExpression("[]"));
            }

            output.Attributes.SetAttribute("node-key", nodeKey);
            output.Attributes.SetAttribute(":props", ToVueExpression($"{{ label: '{nameField}', children: '{childrenField}', disabled: '{disabledField}' }}"));

            if (ShowCheckbox)
                output.Attributes.SetAttribute("show-checkbox", "true");
            if (CheckStrictly)
                output.Attributes.SetAttribute("check-strictly", "true");
            if (DefaultExpandAll)
                output.Attributes.SetAttribute("default-expand-all", "true");
            if (Accordion)
                output.Attributes.SetAttribute("accordion", "true");
            if (HighlightCurrent)
                output.Attributes.SetAttribute("highlight-current", "true");
            if (Draggable)
                output.Attributes.SetAttribute("draggable", "true");

            SetBooleanBinding(output, ":expand-on-click-node", ExpandOnClickNode);
            SetBooleanBinding(output, ":check-on-click-node", CheckOnClickNode);
            SetBooleanBinding(output, ":render-after-expand", RenderAfterExpand);
            SetBooleanBinding(output, ":auto-expand-parent", AutoExpandParent);

            SetJsonBinding(output, ":default-expanded-keys", DefaultExpandedKeys);
            SetJsonBinding(output, ":default-checked-keys", DefaultCheckedKeys);
            SetJsonBinding(output, ":current-node-key", CurrentNodeKey);

            if (Indent.HasValue)
                output.Attributes.SetAttribute(":indent", Indent.Value.ToString());
            if (!string.IsNullOrWhiteSpace(EmptyText))
                output.Attributes.SetAttribute("empty-text", EmptyText.Trim());
            if (!string.IsNullOrWhiteSpace(FilterNodeMethod))
                output.Attributes.SetAttribute(":filter-node-method", ToVueExpression(FilterNodeMethod.Trim()));

            if (!string.IsNullOrWhiteSpace(NodeClick))
                output.Attributes.SetAttribute("v-on:node-click", ToVueExpression(NodeClick.Trim()));
            else if (!string.IsNullOrWhiteSpace(OnNodeClick))
                output.Attributes.SetAttribute("v-on:node-click", BuildNodeClickPostExpression(context, OnNodeClick));
            else if (!string.IsNullOrWhiteSpace(UrlTemplate))
            {
                var navigateExpr = BuildNodeNavigateExpression();
                output.Attributes.SetAttribute("v-on:node-click", navigateExpr);
                output.Attributes.SetAttribute("v-on:current-change", navigateExpr);
            }

            if (!string.IsNullOrWhiteSpace(CurrentChange))
                output.Attributes.SetAttribute("v-on:current-change", ToVueExpression(CurrentChange.Trim()));

            if (!string.IsNullOrWhiteSpace(CheckChange))
                output.Attributes.SetAttribute("v-on:check-change", ToVueExpression(CheckChange.Trim()));
            else if (!string.IsNullOrWhiteSpace(OnCheckChange))
                output.Attributes.SetAttribute("v-on:check-change", BuildCheckChangePostExpression(context, OnCheckChange));

            if (!string.IsNullOrWhiteSpace(slotContent))
                output.Content.SetHtmlContent(slotContent);
        }

        private string BuildNodeClickPostExpression(TagHelperContext context, string handlerName)
        {
            // Do not post the whole `node` object because it contains circular refs and breaks JSON serialization.
            return $"(data, node) => postHandler('{EscapeJs(handlerName)}', {{ eventName: 'node-click', controlId: {ToJsStringOrNull(ResolveControlId(context))}, fieldExpress: {ToJsStringOrNull(ResolveFieldExpress(context))}, value: data, form: (typeof form !== 'undefined' ? form : null) }})";
        }

        private string BuildCheckChangePostExpression(TagHelperContext context, string handlerName)
        {
            return $"(data, checked, indeterminate) => postHandler('{EscapeJs(handlerName)}', {{ eventName: 'check-change', controlId: {ToJsStringOrNull(ResolveControlId(context))}, fieldExpress: {ToJsStringOrNull(ResolveFieldExpress(context))}, value: {{ data: data, checked: checked, indeterminate: indeterminate }}, form: (typeof form !== 'undefined' ? form : null) }})";
        }

        private string BuildNodeNavigateExpression()
        {
            var targetLiteral = ToJsStringOrNull(Target);
            var template = EscapeJs(UrlTemplate ?? string.Empty);
            return $"((d) => {{ d = d || {{}}; const toText = (v) => v == null ? '' : String(v); let url = '{template}'; url = url.split('{{id}}').join(encodeURIComponent(toText(d.id))); url = url.split('{{name}}').join(encodeURIComponent(toText(d.name))); url = url.split('{{label}}').join(encodeURIComponent(toText(d.label))); url = url.split('{{url}}').join(toText(d.url)); if (!url) return; const target = {targetLiteral}; const g = (typeof window !== 'undefined' && window) ? window : ((typeof globalThis !== 'undefined') ? globalThis : null); if (!g) return; if (target) {{ const doc = g.document || null; let iframe = null; if (doc && typeof doc.getElementsByName === 'function') {{ const framesByName = doc.getElementsByName(target); if (framesByName && framesByName.length > 0) iframe = framesByName[0]; }} if (!iframe && doc && typeof doc.querySelector === 'function') {{ iframe = doc.querySelector('iframe[name=\"' + target + '\"]'); }} if (iframe) {{ try {{ iframe.src = url; if (iframe.contentWindow && iframe.contentWindow.location) iframe.contentWindow.location.href = url; return; }} catch (e) {{ }} }} const frame = (g.frames && g.frames[target]) ? g.frames[target] : null; if (frame && frame.location) {{ frame.location.href = url; return; }} if (typeof g.open === 'function') {{ g.open(url, target); return; }} }} if (g.location) g.location.href = url; }})($event)";
        }

        private static void SetBooleanBinding(TagHelperOutput output, string attrName, bool? value)
        {
            if (!value.HasValue)
                return;
            output.Attributes.SetAttribute(attrName, value.Value ? "true" : "false");
        }

        private void SetJsonBinding(TagHelperOutput output, string attrName, object value)
        {
            if (value == null)
                return;

            if (value is string str)
            {
                var text = str.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return;
                output.Attributes.SetAttribute(attrName, ToVueExpression(text));
                return;
            }

            output.Attributes.SetAttribute(attrName, ToVueExpression(value.ToJson()));
        }

        private (string idField, string nameField, string childrenField, string disabledField) ResolveItemsFieldNames(IEnumerable items)
        {
            var idField = IdField ?? NodeKey;
            var nameField = NameField;
            var childrenField = ChildrenField ?? "children";
            var disabledField = DisabledField ?? "disabled";

            var firstItem = items?.Cast<object>().FirstOrDefault(x => x != null);
            if (firstItem == null)
            {
                return (
                    NormalizeFieldForVue(idField ?? "id"),
                    NormalizeFieldForVue(nameField ?? "label"),
                    NormalizeFieldForVue(childrenField),
                    NormalizeFieldForVue(disabledField)
                );
            }

            var props = firstItem.GetType().GetProperties().Select(p => p.Name).ToList();

            idField ??= PickField(props, "id", "value", "key") ?? "id";
            nameField ??= PickField(props, "label", "name", "title", "text") ?? "label";
            childrenField = string.IsNullOrEmpty(ChildrenField)
                ? (PickField(props, "children", "items", "nodes") ?? "children")
                : childrenField;
            disabledField = string.IsNullOrEmpty(DisabledField)
                ? (PickField(props, "disabled", "isDisabled") ?? "disabled")
                : disabledField;

            idField = NormalizeFieldForSerializedJson(props, idField);
            nameField = NormalizeFieldForSerializedJson(props, nameField);
            childrenField = NormalizeFieldForSerializedJson(props, childrenField);
            disabledField = NormalizeFieldForSerializedJson(props, disabledField);

            return (idField, nameField, childrenField, disabledField);
        }

        private string NormalizeFieldForVue(string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;
            return ToCamelCase(field);
        }

        private string NormalizeFieldForSerializedJson(IEnumerable<string> props, string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;

            var match = props.FirstOrDefault(p => string.Equals(p, field, System.StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match))
                return ToCamelCase(match);

            return field;
        }

        private static string PickField(IEnumerable<string> props, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                var match = props.FirstOrDefault(p => string.Equals(p, candidate, System.StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match))
                    return match;
            }

            return null;
        }

        private static string ToCamelCase(string s)
        {
            if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0]))
                return s;

            var i = 0;
            while (i < s.Length && char.IsUpper(s[i]))
                i++;

            if (i == 1)
                return char.ToLowerInvariant(s[0]) + s.Substring(1);

            if (i == s.Length)
                return s.ToLowerInvariant();

            return s.Substring(0, i - 1).ToLowerInvariant() + s.Substring(i - 1);
        }
    }

    public class TreeNodeValue
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Label { get; set; }

        internal static TreeNodeValue Parse(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
                return null;

            return new TreeNodeValue
            {
                Id = ReadString(value, "id"),
                Name = ReadString(value, "name"),
                Label = ReadString(value, "label")
            };
        }

        internal static string ReadString(JsonElement obj, string key)
        {
            if (obj.ValueKind != JsonValueKind.Object)
                return null;

            if (obj.TryGetProperty(key, out var property))
                return property.ToString();
            return null;
        }
    }

    public class TreeNodeClickedEvent
    {
        public string EventName { get; set; }
        public string ControlId { get; set; }
        public string FieldExpress { get; set; }
        public TreeNodeValue Value { get; set; }

        public static TreeNodeClickedEvent Parse(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return new TreeNodeClickedEvent();

            if (root.TryGetProperty("value", out var valueEl))
            {
                return new TreeNodeClickedEvent
                {
                    EventName = TreeNodeValue.ReadString(root, "eventName"),
                    ControlId = TreeNodeValue.ReadString(root, "controlId"),
                    FieldExpress = TreeNodeValue.ReadString(root, "fieldExpress"),
                    Value = TreeNodeValue.Parse(valueEl)
                };
            }

            return new TreeNodeClickedEvent
            {
                // Compatible with payloads that directly post node object as root.
                Value = TreeNodeValue.Parse(root)
            };
        }
    }

    public class TreeNodeChangedValue
    {
        public TreeNodeValue Data { get; set; }
        public bool Checked { get; set; }
        public bool Indeterminate { get; set; }

        internal static TreeNodeChangedValue Parse(JsonElement value)
        {
            if (value.ValueKind != JsonValueKind.Object)
                return null;

            var result = new TreeNodeChangedValue();
            if (value.TryGetProperty("data", out var dataEl))
                result.Data = TreeNodeValue.Parse(dataEl);
            if (value.TryGetProperty("checked", out var checkedEl))
                result.Checked = checkedEl.ValueKind == JsonValueKind.True || (checkedEl.ValueKind == JsonValueKind.String && bool.TryParse(checkedEl.ToString(), out var ck) && ck);
            if (value.TryGetProperty("indeterminate", out var indeterminateEl))
                result.Indeterminate = indeterminateEl.ValueKind == JsonValueKind.True || (indeterminateEl.ValueKind == JsonValueKind.String && bool.TryParse(indeterminateEl.ToString(), out var ind) && ind);
            return result;
        }
    }

    public class TreeNodeChangedEvent
    {
        public string EventName { get; set; }
        public string ControlId { get; set; }
        public string FieldExpress { get; set; }
        public TreeNodeChangedValue Value { get; set; }

        public static TreeNodeChangedEvent Parse(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return new TreeNodeChangedEvent();

            if (root.TryGetProperty("value", out var valueEl))
            {
                return new TreeNodeChangedEvent
                {
                    EventName = TreeNodeValue.ReadString(root, "eventName"),
                    ControlId = TreeNodeValue.ReadString(root, "controlId"),
                    FieldExpress = TreeNodeValue.ReadString(root, "fieldExpress"),
                    Value = TreeNodeChangedValue.Parse(valueEl)
                };
            }

            return new TreeNodeChangedEvent
            {
                // Compatible with payloads that directly post changed value as root.
                Value = TreeNodeChangedValue.Parse(root)
            };
        }
    }
}
