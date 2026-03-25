using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using App.DAL;
using App.Components;
using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace App.EleUI
{
    /// <summary>
    /// 控件基类。封装了一些可视性控制的逻辑。
    /// </summary>
    [HtmlTargetElement("Ele")]
    public abstract class EleControlTagHelper : TagHelper
    {
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }


        [HtmlAttributeName("Width")]
        public string Width { get; set; }

        [HtmlAttributeName("Height")]
        public string Height { get; set; }

        [HtmlAttributeName("Radius")]
        public string Radius { get; set; }

        [HtmlAttributeName("Border")]
        public string Border { get; set; }

        [HtmlAttributeName("BorderColor")]
        public string BorderColor { get; set; }

        [HtmlAttributeName("Rounded")]
        public string Rounded { get; set; }

        [HtmlAttributeName("Shadow")]
        public string Shadow { get; set; }

        [HtmlAttributeName("Disabled")]
        public bool? Disabled { get; set; } // Static disabled state (true/false)

        //
        // 权限控制相关属性
        //
        /// <summary>该控件可见需要的权限</summary>
        [HtmlAttributeName("VPower")]
        public Power VPower { get; set; } = Power.Web;


        //
        // vue 相关属性
        //
        /// <summary>渲染为Vue v-model</summary>
        [HtmlAttributeName("VModel")]
        public string VModel { get; set; }

        [HtmlAttributeName("DisabledFor")]
        public ModelExpression DisabledFor { get; set; } // Strongly typed sugar for disabled binding



        /// <summary>检查权限</summary>
        protected bool CheckPower(TagHelperOutput output)
        {
            if (!Auth.CheckPower(ViewContext.HttpContext, VPower))
            {
                output.SuppressOutput();
                return false;
            }
            return true;
        }


        /// <summary>添加公共属性</summary>
        protected virtual void AddCommonAttributes(TagHelperContext context, TagHelperOutput output)
        {
            // v-model: 数据源
            if (!string.IsNullOrEmpty(VModel))
                output.Attributes.SetAttribute("v-model", VModel);

            // Styles: 样式处理
            var style = output.Attributes["style"]?.Value?.ToString() ?? "";
            if (!string.IsNullOrEmpty(Width))     style += $"width: {Width};";
            if (!string.IsNullOrEmpty(Height))    style += $"height: {Height};";
            if (!string.IsNullOrEmpty(Border))
            {
                var border = Border.Trim();
                if (Regex.IsMatch(border, @"^\d+$")) // 如果是纯数字，默认单位为像素
                    border += "px";
                style += $"border: {border};";
            }
            if (!string.IsNullOrEmpty(BorderColor))
            {
                var color = BorderColor.Trim();
                if (!color.StartsWith("border-") && !color.StartsWith("text-") && !color.StartsWith("bg-"))
                    color = "border-" + color; // 默认当作 border-color 处理
                style += $"--tw-border-opacity: 1; border-color: rgba(var(--{color}), var(--tw-border-opacity));";
            }
            if (!string.IsNullOrEmpty(Radius))
            {
                style += $"border-radius: {Radius};";
                style += $"--el-dialog-border-radius: {Radius};";
            }
            if (!string.IsNullOrEmpty(Shadow))
            {
                if (string.Equals(output.TagName, "el-dialog", StringComparison.OrdinalIgnoreCase))
                {
                    style += $"--el-dialog-box-shadow: {Shadow};";
                }
                else
                {
                    var shadow = Shadow.Trim();
                    if (!string.Equals(shadow, "always", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(shadow, "hover", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(shadow, "never", StringComparison.OrdinalIgnoreCase))
                    {
                        style += $"box-shadow: {shadow};";
                    }
                }
            }

            // style
            if (!string.IsNullOrEmpty(style))
                output.Attributes.SetAttribute("style", style);

            // Enable/Disable
            var disabledForPath = GetBindPath(DisabledFor);
            if (!string.IsNullOrWhiteSpace(disabledForPath))
            {
                var clientPath = ToClientFormPath(disabledForPath);
                var modelType = Nullable.GetUnderlyingType(DisabledFor.ModelExplorer.ModelType) ?? DisabledFor.ModelExplorer.ModelType;
                var disabledExpr = modelType == typeof(bool) ? clientPath : $"!({clientPath})";
                output.Attributes.SetAttribute(":disabled", disabledExpr);
            }
            else
            {
                if (Disabled.HasValue)
                    output.Attributes.SetAttribute(":disabled", Disabled.Value.ToString().ToLower());
            }
        }


        /// <summary>获取Vue绑定路径</summary>
        protected string GetBindPath(ModelExpression expression)
        {
            if (expression == null || string.IsNullOrWhiteSpace(expression.Name))
                return null;

            var path = expression.Name;
            if (path.StartsWith("Model.", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("Model.".Length);

            return path;
        }

        /// <summary>将路径转换为客户端表单路径</summary>
        protected string ToClientFormPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            if (path.StartsWith("Item.", StringComparison.OrdinalIgnoreCase))
            {
                var field = path.Substring("Item.".Length);
                if (!string.IsNullOrEmpty(field) && char.IsUpper(field[0]))
                    field = char.ToLowerInvariant(field[0]) + field.Substring(1);
                return $"form.{field}";
            }

            return path;
        }


        // 辅助方法：将 C# 属性值转换为 CSS 类名，添加到类列表中
        protected static void AddCssClass(List<string> classes, string value, string prefix)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var trimmed = value.Trim();
            if (trimmed.StartsWith(prefix))
                classes.Add(trimmed);
            else
                classes.Add(prefix + trimmed);
        }

        // 辅助方法：处理边框属性，支持多种输入格式，如 "true"、"false"、"1"、"0"、"border" 等
        protected static void AddBorderClass(List<string> classes, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var trimmed = value.Trim();
            var lower = trimmed.ToLowerInvariant();

            if (lower == "false" || lower == "0" || lower == "no" || lower == "off" || lower == "none")
                return;

            if (lower == "true" || lower == "1" || lower == "yes" || lower == "on" || lower == "border")
            {
                classes.Add("border");
                return;
            }

            classes.Add(trimmed.StartsWith("border-") ? trimmed : "border-" + trimmed);
        }        
    }
}
