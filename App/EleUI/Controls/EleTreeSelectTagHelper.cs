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
        public string Api { get; set; }      // Bind from API endpoint data

        [HtmlAttributeName("Items")]
        public object Items { get; set; }    // Bind items to tree select


        [HtmlAttributeName("IdField")]
        public string IdField { get; set; }

        [HtmlAttributeName("ValueField")]
        public string ValueField { get; set; }

        [HtmlAttributeName("NameField")]
        public string NameField { get; set; }

        [HtmlAttributeName("ChildrenField")]
        public string ChildrenField { get; set; }

        [HtmlAttributeName("CheckStrictly")]
        public bool CheckStrictly { get; set; } = true;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            this.Placeholder = string.IsNullOrEmpty(Placeholder) ? "请选择" : Placeholder;
            output.TagName = "el-tree-select";
            AddCommonAttributes(context, output);

            // set data-tree-model
            var propName = GetPropName();
            if (!string.IsNullOrEmpty(propName))
                output.Attributes.SetAttribute("data-tree-model", propName);

            // check-strictly
            if (CheckStrictly)
                output.Attributes.SetAttribute("check-strictly", "true");
        
            // fields
            var idField = NormalizeFieldForVue(IdField ?? ValueField ?? "id");
            var nameField = NormalizeFieldForVue(NameField ?? "name");
            var childrenField = NormalizeFieldForVue(ChildrenField ?? "children");

            // Remote data source via API
            if (!string.IsNullOrEmpty(Api))
            {
                var url = Api;
                output.Attributes.SetAttribute(":data", $"options['{propName}']");                
                output.Attributes.SetAttribute("data-source", url);
                output.Attributes.SetAttribute("data-key", propName);
                output.Attributes.SetAttribute("data-tree-id-field", idField);
                output.Attributes.SetAttribute("data-tree-children-field", childrenField);
                output.Attributes.SetAttribute("node-key", idField);
                output.Attributes.SetAttribute(":props", $"{{ label: '{nameField}', children: '{childrenField}', value: '{idField}' }}");
                output.Attributes.SetAttribute("default-expand-all", "true");
                output.Attributes.SetAttribute(":key", $"options['{propName}'] ? options['{propName}'].length : 0");
            }

            // Use items
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

            await RenderWrapper(output);
        }

        // Get object field value by trying multiple possible field names
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

        // Normalize field name to Vue case
        private string NormalizeFieldForVue(string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;
            return ToCamelCase(field);
        }

        // Normalize field name to serialized JSON case
        private string NormalizeFieldForSerializedJson(IEnumerable<string> props, string field)
        {
            if (string.IsNullOrEmpty(field))
                return field;

            var match = props.FirstOrDefault(p => string.Equals(p, field, System.StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(match))
                return ToCamelCase(match);

            return field;
        }

        // Pick field name from candidates
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
