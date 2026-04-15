using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using App.Utils;
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

        [HtmlAttributeName("Enabled")]
        public bool Enabled { get; set; } = true; // Static enabled state (true/false)

        [HtmlAttributeName("EnabledFor")]
        public ModelExpression EnabledFor { get; set; } // Strongly typed sugar for enabled binding

        /// <summary>控件是否可见。默认 true</summary>
        [HtmlAttributeName("Visible")]
        public bool Visible { get; set; } = true;

        //
        // vue 相关属性
        //
        /// <summary>渲染为Vue v-model</summary>
        [HtmlAttributeName("VModel")]
        public string VModel { get; set; }


        /// <summary>检查可见性</summary>
        protected bool CheckPower(TagHelperOutput output)
        {
            if (!Visible)
            {
                output.SuppressOutput();
                return false;
            }

            return true;
        }

        protected bool IsSelectMode()
        {
            var md = ViewContext?.HttpContext?.Request?.Query["md"].ToString();
            if (string.IsNullOrWhiteSpace(md))
                return false;

            if (Enum.TryParse<PageMode>(md, true, out var mode))
                return mode == PageMode.Select;

            if (int.TryParse(md, out var modeValue))
                return ((PageMode)modeValue) == PageMode.Select;

            return string.Equals(md, "select", StringComparison.OrdinalIgnoreCase);
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
            var enabledForPath = GetBindPath(EnabledFor);
            if (!string.IsNullOrWhiteSpace(enabledForPath))
            {
                var clientPath = ToClientFormPath(enabledForPath);
                var disabledExpr = $"!({clientPath})";
                output.Attributes.SetAttribute(":disabled", disabledExpr);
            }
            else
            {
                if (context.AllAttributes.ContainsName("Enabled"))
                    output.Attributes.SetAttribute(":disabled", (!Enabled).ToString().ToLower());
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
