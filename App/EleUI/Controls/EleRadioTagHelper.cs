using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using App.Utils;

namespace App.EleUI
{
    [HtmlTargetElement("EleRadio")]
    public class EleRadioTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Items")]
        public string Items { get; set; }

        [HtmlAttributeName("Data")]
        public object Data { get; set; }

        [HtmlAttributeName("IsButton")]
        public bool IsButton { get; set; } = false;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "el-radio-group";
            AddCommonAttributes(context, output);

            var childContent = await output.GetChildContentAsync();
            var contentHtml = "";
            string tag = IsButton ? "el-radio-button" : "el-radio";

            // 1. Generate options from Enum if For is Enum
            if (For != null)
            {
                var type = For.ModelExplorer.ModelType;
                type = Nullable.GetUnderlyingType(type) ?? type;
                if (type.IsEnum)
                {
                    foreach (var value in Enum.GetValues(type))
                    {
                        var info = App.Utils.EnumHelper.GetEnumInfo(value);
                        contentHtml += $@"<{tag} :label=""{(int)value}"">{info.Title}</{tag}>";
                    }
                }
            }

            // 2. Generate options from Items
            if (!string.IsNullOrEmpty(Items) || Data != null)
            {
                var list = new List<SelectListItem>();
                
                // 2.1 Parse string Items (JSON array, JSON object, or comma-separated)
                if (!string.IsNullOrEmpty(Items))
                {
                    var strItems = Items.Trim();
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
                
                // 2.2 Parse Data object
                if (Data != null)
                {
                    if (Data is IEnumerable<SelectListItem> selectList)
                    {
                        list.AddRange(selectList);
                    }
                    else if (Data is IDictionary dictionary)
                    {
                        foreach (DictionaryEntry item in dictionary)
                        {
                            list.Add(new SelectListItem(item.Key.ToString(), item.Value.ToString()));
                        }
                    }
                     else if (Data is IEnumerable enumerable)
                    {
                        foreach (var item in enumerable)
                        {
                            list.Add(new SelectListItem(item.ToString(), item.ToString()));
                        }
                    }
                }

                foreach (var item in list)
                {
                    // Determine target type from For expression to ensure correct binding type (string vs number vs bool)
                    var targetType = For?.ModelExplorer.ModelType;
                    if (targetType != null)
                        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                    string labelAttr;
                    
                    if (targetType == typeof(int) || targetType == typeof(long) || targetType == typeof(short) || 
                        targetType == typeof(double) || targetType == typeof(decimal) || targetType == typeof(float))
                    {
                         // Force numeric binding
                         labelAttr = $@":label=""{item.Value}""";
                    }
                    else if (targetType == typeof(bool))
                    {
                        // Force boolean binding
                        labelAttr = $@":label=""{item.Value.ToLower()}""";
                    }
                    else if (targetType == typeof(string) || targetType == typeof(char))
                    {
                         // Force string binding
                         labelAttr = $@"label=""{item.Value}""";
                    }
                    else
                    {
                        // Fallback: guess type based on value content
                        bool isNumeric = double.TryParse(item.Value, out double numVal);
                        bool isBool = bool.TryParse(item.Value, out bool boolVal);
                        
                        if (isNumeric)
                            labelAttr = $@":label=""{item.Value}"""; 
                        else if (isBool)
                             labelAttr = $@":label=""{item.Value.ToLower()}""";
                        else
                            labelAttr = $@"label=""{item.Value}""";
                    }

                    contentHtml += $@"<{tag} {labelAttr}>{item.Text}</{tag}>";
                }
            }
            
            // 3. Append child content (manual options)
            contentHtml += childContent.GetContent();

            output.Content.SetHtmlContent(contentHtml);
            await RenderWrapper(output);
        }
    }
}
