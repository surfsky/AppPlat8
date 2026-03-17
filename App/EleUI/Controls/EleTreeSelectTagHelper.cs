using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using App.Utils;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.EleUI
{
    [HtmlTargetElement("EleTreeSelect")]
    public class EleTreeSelectTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Api")]
        public string Api { get; set; }      // API endpoint for remote data fetch

        [HtmlAttributeName("DataName")]
        public string DataName { get; set; } // Property name for binding data source name

        [HtmlAttributeName("Items")]
        public object Items { get; set; }    // Optional direct items for non-API, non-Source usage


        [HtmlAttributeName("IdField")]
        public string IdField { get; set; }

        [HtmlAttributeName("ValueField")]
        public string ValueField { get; set; }

        [HtmlAttributeName("NameField")]
        public string NameField { get; set; }

        [HtmlAttributeName("ChildrenField")]
        public string ChildrenField { get; set; }

        [HtmlAttributeName("Placeholder")]
        public string Placeholder { get; set; }

        [HtmlAttributeName("CheckStrictly")]
        public bool CheckStrictly { get; set; } = true;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            output.TagName = "el-tree-select";
            AddCommonAttributes(context, output);
            var propName = GetPropName();
            if (!string.IsNullOrEmpty(propName))
            {
                output.Attributes.SetAttribute("data-tree-model", propName);
            }
            if (CheckStrictly)
            {
                output.Attributes.SetAttribute("check-strictly", "true");
            }

        
            // If Api is provided, use it for remote data fetch
            var idField = NormalizeFieldForVue(IdField ?? ValueField ?? "id");
            var nameField = NormalizeFieldForVue(NameField ?? "name");
            var childrenField = NormalizeFieldForVue(ChildrenField ?? "children");
            if (!string.IsNullOrEmpty(Api))
            {
                var url = Api;
                // Bind to options dictionary
                // Add marker attributes for auto-fetch
                output.Attributes.SetAttribute(":data", $"options['{propName}']");                
                output.Attributes.SetAttribute("data-source", url);
                output.Attributes.SetAttribute("data-key", propName);
                output.Attributes.SetAttribute("data-tree-id-field", idField);
                output.Attributes.SetAttribute("data-tree-children-field", childrenField);
                output.Attributes.SetAttribute("node-key", idField);
                output.Attributes.SetAttribute(":props", $"{{ label: '{nameField}', children: '{childrenField}', value: '{idField}' }}");
                output.Attributes.SetAttribute("default-expand-all", "true");
                // when options arrive asynchronously we want the tree-select to recreate so that
                // it can map the initial value to the corresponding label. binding a key that
                // changes when the data array length changes forces a rerender.
                output.Attributes.SetAttribute(":key", $"options['{propName}'] ? options['{propName}'].length : 0");
            }

            // If Source is provided, use it for local data binding
            else if (!string.IsNullOrEmpty(DataName))
            {
                // Direct data binding via Model property name (e.g. OrgTree)
                // Use provided fields or default to value/label (standard Select/TreeSelect behavior)
                // If user doesn't provide IdField/NameField, we default to 'value' and 'label' for Source mode
                // because typically local data is formatted for the component.
                var sourceIdField   = NormalizeFieldForVue(IdField ?? ValueField ?? "value");
                var sourceNameField = NormalizeFieldForVue(NameField ?? "label");
                var sourceChildrenField = NormalizeFieldForVue(ChildrenField ?? "children");
                output.Attributes.SetAttribute(":data", DataName);
                output.Attributes.SetAttribute("data-tree-id-field", sourceIdField);
                output.Attributes.SetAttribute("data-tree-children-field", sourceChildrenField);
                output.Attributes.SetAttribute("node-key", sourceIdField);
                output.Attributes.SetAttribute(":props", $"{{ label: '{sourceNameField}', children: '{sourceChildrenField}', value: '{sourceIdField}' }}");
                output.Attributes.SetAttribute("default-expand-all", "true");
                output.Attributes.SetAttribute(":key", $"({DataName} || []).length");
            }

            // If Items are provided, use them for local data binding
            else if (Items != null)
            {
                if (Items is IEnumerable enumerable && Items is not string)
                {
                    var (sourceIdField, sourceNameField, sourceChildrenField) = ResolveItemsFieldNames(enumerable);
                    var jsonItems = Items.ToJson();
                    output.Attributes.SetAttribute(":data", $"normalizeIds({jsonItems})");
                    output.Attributes.SetAttribute("data-static-items", jsonItems);
                    output.Attributes.SetAttribute("data-tree-id-field", sourceIdField);
                    output.Attributes.SetAttribute("data-tree-children-field", sourceChildrenField);
                    output.Attributes.SetAttribute("node-key", sourceIdField);
                    output.Attributes.SetAttribute(":props", $"{{ label: '{sourceNameField}', children: '{sourceChildrenField}', value: '{sourceIdField}' }}");
                    output.Attributes.SetAttribute("default-expand-all", "true");
                    output.Attributes.SetAttribute(":key", $"({jsonItems} || []).length");
                }
            }

            // Set placeholder and default width
            if (!string.IsNullOrEmpty(Placeholder)) output.Attributes.SetAttribute("placeholder", Placeholder);
            if (string.IsNullOrEmpty(Width))        output.Attributes.SetAttribute("class", "w-full");
            await RenderWrapper(output);
        }

        private (string idField, string nameField, string childrenField) ResolveItemsFieldNames(IEnumerable items)
        {
            var idField = IdField ?? ValueField;
            var nameField = NameField;
            var childrenField = ChildrenField ?? "children";

            var firstItem = items.Cast<object>().FirstOrDefault(x => x != null);
            if (firstItem == null)
                return (
                    NormalizeFieldForVue(idField ?? "id"),
                    NormalizeFieldForVue(nameField ?? "name"),
                    NormalizeFieldForVue(childrenField)
                );

            var props = firstItem.GetType().GetProperties().Select(p => p.Name).ToList();

            idField ??= PickField(props, "id", "value") ?? "id";
            nameField ??= PickField(props, "name", "label") ?? "name";
            childrenField = string.IsNullOrEmpty(ChildrenField)
                ? (PickField(props, "children") ?? "children")
                : childrenField;

            idField = NormalizeFieldForSerializedJson(props, idField);
            nameField = NormalizeFieldForSerializedJson(props, nameField);
            childrenField = NormalizeFieldForSerializedJson(props, childrenField);

            return (idField, nameField, childrenField);
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
    }
}
