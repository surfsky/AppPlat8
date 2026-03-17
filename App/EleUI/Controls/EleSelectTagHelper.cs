using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using App.Utils;

namespace App.EleUI
{
    [HtmlTargetElement("EleSelect")]
    public class EleSelectTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Items")]
        public object Items { get; set; }

        [HtmlAttributeName("Placeholder")]
        public string Placeholder { get; set; }

        [HtmlAttributeName("Multiple")]
        public bool Multiple { get; set; }

        [HtmlAttributeName("AllowCreate")]
        public bool AllowCreate { get; set; }

        [HtmlAttributeName("Filterable")]
        public bool Filterable { get; set; }

        [HtmlAttributeName("KeyField")]
        public string KeyField { get; set; }

        [HtmlAttributeName("TextField")]
        public string TextField { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "el-select";
            AddCommonAttributes(context, output);
            if (!string.IsNullOrEmpty(Placeholder)) output.Attributes.SetAttribute("placeholder", Placeholder);
            if (string.IsNullOrEmpty(Width)) output.Attributes.SetAttribute("class", "w-full");
            if (Multiple) output.Attributes.SetAttribute("multiple", "true");
            if (AllowCreate) output.Attributes.SetAttribute("allow-create", "true");
            if (Filterable) output.Attributes.SetAttribute("filterable", "true");

            var childContent = await output.GetChildContentAsync();
            var contentHtml = "";

            // 1. Generate options from Enum if For is Enum
            if (For != null)
            {
                var type = For.ModelExplorer.ModelType;
                type = Nullable.GetUnderlyingType(type) ?? type;
                
                // Handle List<Enum> or Enum[] for multiple select
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

                if (type.IsEnum)
                {
                    foreach (var value in Enum.GetValues(type))
                    {
                        var info = App.Utils.EnumHelper.GetEnumInfo(value);
                        contentHtml += $@"<el-option label=""{info.Title}"" :value=""{(int)info.Value}""></el-option>";
                    }
                }
            }

            // 2. Generate options from Items
            if (Items != null)
            {
                var list = new List<SelectListItem>();
                if (Items is IEnumerable<SelectListItem> selectList)
                {
                    list.AddRange(selectList);
                }
                else if (Items is string strItems)
                {
                    strItems = strItems.Trim();
                    // Handle ['A', 'B'] array
                    if (strItems.StartsWith("[") && strItems.EndsWith("]"))
                    {
                        try 
                        {
                            var json = strItems.Replace("'", "\"");
                            var jArray = JArray.Parse(json); 
                            foreach (var item in jArray)
                            {
                                var val = item.ToString();
                                list.Add(new SelectListItem(val, val));
                            }
                        }
                        catch {}
                    }
                    // Handle {'A':1, 'B':0} object
                    else if (strItems.StartsWith("{") && strItems.EndsWith("}"))
                    {
                         try 
                        {
                            var json = strItems.Replace("'", "\"");
                            var jObject = JObject.Parse(json);
                            foreach (var item in jObject)
                            {
                                list.Add(new SelectListItem(item.Key, item.Value.ToString()));
                            }
                        }
                        catch {}
                    }
                    // Handle "A, B, C" simple string
                    else
                    {
                        var arr = strItems.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach(var item in arr)
                        {
                             var val = item.Trim();
                             if (!string.IsNullOrEmpty(val))
                             {
                                 list.Add(new SelectListItem(val, val));
                             }
                        }
                    }
                }
                else if (Items is IDictionary dictionary)
                {
                    foreach (DictionaryEntry item in dictionary)
                    {
                        list.Add(new SelectListItem(item.Key.ToString(), item.Value.ToString()));
                    }
                }
                // Handle EleUI.ListItem (Label/Value) produced by EleHelper.ToItems
                else if (Items is IEnumerable<ListItem> eleList)
                {
                    foreach (var item in eleList)
                    {
                        var val = item.Value == null ? "" : item.Value.ToString();
                        list.Add(new SelectListItem(item.Label, val));
                    }
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
                        {
                            value = item.ToString();
                        }

                        list.Add(new SelectListItem(text ?? value, value));
                    }
                }

                // 3. Generate options from list
                foreach (var item in list)
                {
                    // Determine target type from For expression
                    var targetType = For?.ModelExplorer.ModelType;
                    if (targetType != null)
                    {
                        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                        // Handle List<T> or T[] for multiple select
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
                    }

                    string valueAttr;
                    
                    if (targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short) || 
                        targetType == typeof(double) || targetType == typeof(decimal) || targetType == typeof(float))
                    {
                         // Force numeric binding
                         valueAttr = $@":value=""{item.Value}""";
                    }
                    else if (targetType == typeof(bool))
                    {
                        // Force boolean binding
                        valueAttr = $@":value=""{item.Value.ToLower()}""";
                    }
                    else if (targetType == typeof(string) || targetType == typeof(char))
                    {
                         // Force string binding
                         valueAttr = $@"value=""{item.Value}""";
                    }
                    else
                    {
                        // Fallback: guess type based on value content
                        bool isNumeric = double.TryParse(item.Value, out double numVal);
                        bool isBool = bool.TryParse(item.Value, out bool boolVal);
                        
                        if (isNumeric)
                            valueAttr = $@":value=""{item.Value}"""; 
                        else if (isBool)
                             valueAttr = $@":value=""{item.Value.ToLower()}""";
                        else
                            valueAttr = $@"value=""{item.Value}""";
                    }

                    contentHtml += $@"<el-option label=""{item.Text}"" {valueAttr}></el-option>";
                }
            }
            
            // 3. Append child content (manual options)
            contentHtml += childContent.GetContent();
            output.Content.SetHtmlContent(contentHtml);
            await RenderWrapper(output);
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
