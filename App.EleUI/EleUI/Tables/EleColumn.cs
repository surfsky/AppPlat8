using App.Components;
using System;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    public enum ColumnDisplay
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
    public class EleColumn : EleColumnBase
    {
        /// <summary>绑定的模型表达式，用于自动解析属性名</summary>
        [HtmlAttributeName("For")]
        public ModelExpression For { get; set; }

        /// <summary>绑定的属性名</summary>
        [HtmlAttributeName("Prop")]
        public string Prop { get; set; }

        /// <summary>列展示格式，默认自动识别属性类型</summary>
        [HtmlAttributeName("Display")]
        public ColumnDisplay Display { get; set; } = ColumnDisplay.Auto;

        /// <summary>自定义格式化字符串，仅当Display为Custom时生效</summary>
        [HtmlAttributeName("FormatString")]
        public string FormatString { get; set; }

        /// <summary>若为true，若文本过长则自动换行</summary>
        [HtmlAttributeName("Wrap")]
        public bool Wrap { get; set; } = false;

        /// <summary>若为true，则展示为超链接，点击后跳转到详情页</summary>
        [HtmlAttributeName("Link")]
        public bool Link { get; set; }

        /// <summary>弹窗页面URL，支持{xxx}占位符（如{id}、{fileName}），点击列文本后使用EleManager.Drawer打开</summary>
        [HtmlAttributeName("PopupUrl")]
        public string PopupUrl { get; set; }

        /// <summary>弹窗标题，留空时使用列Label</summary>
        [HtmlAttributeName("PopupTitle")]
        public string PopupTitle { get; set; }

        /// <summary>弹窗尺寸（百分比或像素），默认为空（即宽屏50%，窄屏全屏）</summary>
        [HtmlAttributeName("PopupSize")]
        public string PopupSize { get; set; } = "";

        /// <summary>弹窗方向，默认rtl（从右向左滑出）</summary>
        [HtmlAttributeName("PopupDirection")]
        public string PopupDirection { get; set; } = "rtl";


        public EleColumn()
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
                if (Display == ColumnDisplay.Auto)
                {
                    // Handle Nullable types
                    var type = For.ModelExplorer.ModelType;
                    if (Nullable.GetUnderlyingType(type) != null)
                        type = Nullable.GetUnderlyingType(type);
                    if (type == typeof(bool))
                        Display = ColumnDisplay.Tag;
                    else if (type == typeof(DateTime))
                        Display = ColumnDisplay.DateTime;
                    else if (type.IsEnum)
                        Display = ColumnDisplay.Enum;
                }
            }

            if (!string.IsNullOrEmpty(propName))
                output.Attributes.SetAttribute("prop", propName);
            ApplyBaseColumnAttributes(output, labelText);

            output.Attributes.SetAttribute("class-name", Wrap ? "ele-col-wrap" : "ele-col-nowrap");
            if (!Wrap)
            {
                // Use Element Plus built-in overflow tooltip so full text appears on hover.
                output.Attributes.SetAttribute("show-overflow-tooltip", "true");
            }

            // 2. Handle Template Content
            var childContent = await output.GetChildContentAsync();
            if (!childContent.IsEmptyOrWhiteSpace)
            {
                output.Content.SetHtmlContent(childContent);
            }
            else
            {
                // Default Templates
                if (!string.IsNullOrEmpty(PopupUrl))
                {
                    output.Content.SetHtmlContent(BuildPopupTemplate(propName));
                }
                else if (Link)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span class=""text-blue-600 cursor-pointer"" v-on:click=""openView(scope.row.id)"">{{{{ scope.row.{propName} ?? '' }}}}</span>
                        </template>
                    ");
                }
                else if (Display == ColumnDisplay.Custom || !string.IsNullOrEmpty(FormatString))
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
                else if (Display == ColumnDisplay.DateTime)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span>{{{{ Utils.formatDate(scope.row.{propName}, 'DateTime') }}}}</span>
                        </template>
                    ");
                }
                else if (Display == ColumnDisplay.Date)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span>{{{{ Utils.formatDate(scope.row.{propName}, 'Date') }}}}</span>
                        </template>
                    ");
                }
                else if (Display == ColumnDisplay.Time)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <span>{{{{ Utils.formatDate(scope.row.{propName}, 'Time') }}}}</span>
                        </template>
                    ");
                }
                else if (Display == ColumnDisplay.Switch)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <el-switch v-model=""scope.row.{propName}"" disabled />
                        </template>
                    ");
                }
                else if (Display == ColumnDisplay.Tag)
                {
                    output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <el-tag v-if=""scope.row.{propName}"" type=""success"">是</el-tag>
                            <el-tag v-else type=""info"">否</el-tag>
                        </template>
                    ");
                }
                else if (Display == ColumnDisplay.Enum)
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
                        var json = JsonSerializer.Serialize(options);
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

        /// <summary>构造Popup超链接模板</summary>
        private string BuildPopupTemplate(string propName)
        {
            var urlExpr = BuildPopupUrlExpr(PopupUrl);
            var titleExpr = BuildPopupTextExpr(!string.IsNullOrEmpty(PopupTitle) ? PopupTitle : (Label ?? "查看"));
            var dir = EscapeSingleQuoted(PopupDirection ?? "rtl");
            var popupSize = PopupSize?.Trim();
            var openDrawerArgs = !string.IsNullOrEmpty(popupSize)
                ? $"{urlExpr}, '{EscapeSingleQuoted(popupSize)}', '{dir}', {titleExpr}"
                : $"{urlExpr}, null, '{dir}', {titleExpr}";

            return $@"
                        <template #default=""scope"">
                            <span class=""text-blue-600 cursor-pointer hover:text-blue-700 no-underline"" @click=""openDrawer({openDrawerArgs})"">
                                {{{{ scope.row.{propName} ?? '' }}}}
                            </span>
                        </template>
                    ";
        }

        /// <summary>将Popup Url模板字符串（含{id}占位符）编译为JS拼接表达式</summary>
        private static string BuildPopupUrlExpr(string template)
        {
            if (string.IsNullOrEmpty(template))
                return "''";

            var matches = Regex.Matches(template, "\\{([A-Za-z_][A-Za-z0-9_\\.]*)\\}");
            if (matches.Count == 0)
                return $"'{EscapeSingleQuoted(template)}'";

            var sb = new StringBuilder();
            var last = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var literal = template.Substring(last, m.Index - last);
                if (!string.IsNullOrEmpty(literal))
                {
                    if (sb.Length > 0) sb.Append(" + ");
                    sb.Append("'").Append(EscapeSingleQuoted(literal)).Append("'");
                }

                var token = m.Groups[1].Value;
                var path = token.StartsWith("scope.") ? token : $"scope.row.{token}";
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append($"encodeURIComponent((({path}) ?? '').toString())");
                last = m.Index + m.Length;
            }

            var tail = template.Substring(last);
            if (!string.IsNullOrEmpty(tail))
            {
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append("'").Append(EscapeSingleQuoted(tail)).Append("'");
            }

            return sb.Length == 0 ? "''" : sb.ToString();
        }

        /// <summary>将Popup标题模板字符串（支持{fileName}占位符）编译为JS拼接表达式</summary>
        private static string BuildPopupTextExpr(string template)
        {
            if (string.IsNullOrEmpty(template))
                return "''";

            var matches = Regex.Matches(template, "\\{([A-Za-z_][A-Za-z0-9_\\.]*)\\}");
            if (matches.Count == 0)
                return $"'{EscapeSingleQuoted(template)}'";

            var sb = new StringBuilder();
            var last = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var literal = template.Substring(last, m.Index - last);
                if (!string.IsNullOrEmpty(literal))
                {
                    if (sb.Length > 0) sb.Append(" + ");
                    sb.Append("'").Append(EscapeSingleQuoted(literal)).Append("'");
                }

                var token = m.Groups[1].Value;
                var path = token.StartsWith("scope.") ? token : $"scope.row.{token}";
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append($"((({path}) ?? '').toString())");
                last = m.Index + m.Length;
            }

            var tail = template.Substring(last);
            if (!string.IsNullOrEmpty(tail))
            {
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append("'").Append(EscapeSingleQuoted(tail)).Append("'");
            }

            return sb.Length == 0 ? "''" : sb.ToString();
        }

        /// <summary>转义单引号包围的JS字符串</summary>
        private static string EscapeSingleQuoted(string text)
        {
            return (text ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
        }
    }
}
