using App.DAL;
using App.Components;
using System;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using App.Utils; 
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    //-----------------------------------------------------------------
    // Columns
    //-----------------------------------------------------------------
    /// <summary>表格列展示格式</summary>
    public enum ColumnFormat
    {
        Auto, // 自动识别
        Text,
        DateTime,
        Date,
        Time,
        Switch, // 开关
        Tag, // 标签
        Enum, // 枚举
        Custom // 自定义格式化字符串
    }


    /// <summary>文本列</summary> 
    [HtmlTargetElement("EleColumn", ParentTag = "Columns")] 
    public class EleColumnTagHelper : EleColumnBaseTagHelper
    {
        [HtmlAttributeName("For")]
        public ModelExpression For { get; set; }

        [HtmlAttributeName("Prop")]
        public string Prop { get; set; }

        [HtmlAttributeName("Format")]
        public ColumnFormat Format { get; set; } = ColumnFormat.Auto;

        [HtmlAttributeName("FormatString")]
        public string FormatString { get; set; }

        [HtmlAttributeName("Link")]
        public bool Link { get; set; }

        public EleColumnTagHelper()
        {
            Sortable = true;
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckVisible(output))
                return;

            SetupColumnShell(output);

            string propName = Prop;
            string labelText = Label;

            // 1. Resolve Prop and Label from Expression if not provided
            if (For != null)
            {
                if (string.IsNullOrEmpty(propName))
                {
                    propName = For.Metadata.PropertyName ?? For.Name;
                    if (propName.Contains(".")) 
                        propName = propName.Substring(propName.LastIndexOf('.') + 1);
                    
                    if (!string.IsNullOrEmpty(propName) && char.IsUpper(propName[0]))
                    {
                        propName = char.ToLower(propName[0]) + propName.Substring(1);
                    }
                }

                if (string.IsNullOrEmpty(labelText))
                {
                    labelText = For.Metadata.DisplayName ?? For.Metadata.PropertyName ?? propName;
                }

                // Auto-detect Format if is Auto
                if (Format == ColumnFormat.Auto)
                {
                    // Handle Nullable types
                    var type = For.ModelExplorer.ModelType;
                    if (Nullable.GetUnderlyingType(type) != null)
                        type = Nullable.GetUnderlyingType(type);
                    if (type == typeof(bool))
                        Format = ColumnFormat.Switch;
                    else if (type == typeof(DateTime))
                        Format = ColumnFormat.DateTime;
                    else if (type.IsEnum)
                        Format = ColumnFormat.Enum;
                }
            }

            if (!string.IsNullOrEmpty(propName))
                output.Attributes.SetAttribute("prop", propName);
            ApplyBaseColumnAttributes(output, labelText);

            // 2. Handle Template Content
            var childContent = await output.GetChildContentAsync();
            if (!childContent.IsEmptyOrWhiteSpace)
            {
                output.Content.SetHtmlContent(childContent);
            }
            else
            {
                // Default Templates
                if (Link)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span class=""text-blue-600 cursor-pointer"" v-on:click=""openView(scope.row.id)"">{{{{ scope.row.{propName} }}}}</span>
                        </template>
                    ");
                }
                else if (Format == ColumnFormat.Custom || !string.IsNullOrEmpty(FormatString))
                {
                     // If FormatString is provided (e.g. yyyy-MM-dd), we assume Date/Time formatting for now
                     // Or it could be a JS formatter function name if we supported that.
                     // For now, let's map standard C# date format strings to our JS helper
                     // Simple mapping logic:
                     string jsType = "DateTime"; // Default
                     if (!string.IsNullOrEmpty(FormatString))
                     {
                         if (FormatString.Contains("H") || FormatString.Contains("m") || FormatString.Contains("s")) jsType = "DateTime";
                         else if (FormatString.Contains("y") || FormatString.Contains("M") || FormatString.Contains("d")) jsType = "Date";
                     }
                     
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span>{{{{ Utils.formatDate(scope.row.{propName}, '{jsType}') }}}}</span>
                        </template>
                    ");
                }
                else if (Format == ColumnFormat.DateTime)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span>{{{{ Utils.formatDate(scope.row.{propName}, 'DateTime') }}}}</span>
                        </template>
                    ");
                }
                else if (Format == ColumnFormat.Date)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span>{{{{ Utils.formatDate(scope.row.{propName}, 'Date') }}}}</span>
                        </template>
                    ");
                }
                else if (Format == ColumnFormat.Time)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span>{{{{ Utils.formatDate(scope.row.{propName}, 'Time') }}}}</span>
                        </template>
                    ");
                }
                else if (Format == ColumnFormat.Switch)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <el-switch v-model=""scope.row.{propName}"" disabled />
                        </template>
                    ");
                }
                else if (Format == ColumnFormat.Tag)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <el-tag v-if=""scope.row.{propName}"" type=""success"">是</el-tag>
                            <el-tag v-else type=""info"">否</el-tag>
                        </template>
                    ");
                }
                else if (Format == ColumnFormat.Enum)
                {
                    // 获取枚举类型
                    Type enumType = null;
                    if (For != null)
                    {
                        var type = For.ModelExplorer.ModelType;
                        enumType = Nullable.GetUnderlyingType(type) ?? type;
                    }
                    if (enumType != null && enumType.IsEnum)
                    {
                        // TODO：这段代码展示有问题，请修正
                        var options = App.Utils.EnumHelper.GetEnumInfos(enumType);
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(options);                        
                        output.Content.SetHtmlContent($@"
                            <template #default=""scope"">
                                <span>{{{{ Utils.formatEnum(scope.row.{propName}, {json}) }}}}</span>
                            </template>
                        ");
                    }
                }
                else 
                {
                    output.Content.SetHtmlContent("");
                }
            }
        }
    }
}
