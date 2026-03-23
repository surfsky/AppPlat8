using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using App.DAL;
using App.Components;
using System;
using System.Text.RegularExpressions;

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

        [HtmlAttributeName("Shadow")]
        public string Shadow { get; set; }

        [HtmlAttributeName("Disabled")]
        public bool? Disabled { get; set; } // Static disabled state (true/false)

        /// <summary>该控件可见需要的权限</summary>
        [HtmlAttributeName("VPower")]
        public Power VPower { get; set; } = Power.Web;


        //
        // vue 相关属性
        //
        /// <summary>渲染为Vue v-model</summary>
        [HtmlAttributeName("VModel")]
        public string VModel { get; set; }

        /// <summary>渲染为Vue v-if</summary>
        [HtmlAttributeName("VIf")]
        public string VIf { get; set; }

        /// <summary>强类型渲染为Vue v-if</summary>
        [HtmlAttributeName("VIfFor")]
        public ModelExpression VIfFor { get; set; }

        /// <summary>渲染为Vue v-show</summary>
        [HtmlAttributeName("VShow")]
        public string VShow { get; set; }

        /// <summary>强类型渲染为Vue v-show</summary>
        [HtmlAttributeName("VShowFor")]
        public ModelExpression VShowFor { get; set; }


        [HtmlAttributeName("BindDisabled")]
        public string BindDisabled { get; set; } // Dynamic disabled expression (v-bind:disabled)

        [HtmlAttributeName("BindDisabledFor")]
        public ModelExpression BindDisabledFor { get; set; } // Strongly typed dynamic disabled expression

        /// <summary>
        /// 启用条件（自动转换为 :disabled="!(...)"）。
        /// 示例：EnableFor="Item.Id > 0"。
        /// </summary>
        [HtmlAttributeName("EnableFor")]
        public string EnableFor { get; set; }


        /*
        /// <summary>处理标签</summary>
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "div";
            AddCommonAttributes(context, output);
            var childContent = await output.GetChildContentAsync();
            var innerHtml = childContent.GetContent();
            output.Content.SetHtmlContent(innerHtml);
        }
        */


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
            if (!string.IsNullOrEmpty(VModel))
                output.Attributes.SetAttribute("v-model", VModel);

            var vIfExpr = GetClientBindPath(VIfFor);
            if (string.IsNullOrWhiteSpace(vIfExpr))
                vIfExpr = VIf;
            if (!string.IsNullOrEmpty(vIfExpr))
                output.Attributes.SetAttribute("v-if", vIfExpr);

            var vShowExpr = GetClientBindPath(VShowFor);
            if (string.IsNullOrWhiteSpace(vShowExpr))
                vShowExpr = VShow;
            if (!string.IsNullOrEmpty(vShowExpr))
                output.Attributes.SetAttribute("v-show", vShowExpr);

            // Style handling
            var style = output.Attributes["style"]?.Value?.ToString() ?? "";
            if (!string.IsNullOrEmpty(Width))
                style += $"width: {Width};";
            if (!string.IsNullOrEmpty(Height))
                style += $"height: {Height};";
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
            if (!string.IsNullOrEmpty(style))
                output.Attributes.SetAttribute("style", style);

            // Class handling
            // Razor TagHelper automatically merges "class" attribute if present in cshtml and in code?
            // Yes, standard behavior is merge for class. 
            // So if user writes <ElePanel class="h-[200px]"> and we don't set class, it's preserved.
            // If we set class, it merges.

            // Disabled handling
            // Priority: EnableFor > BindDisabled > Disabled > "readOnly" variable (if none set)
            // But base class shouldn't assume "readOnly" variable exists unless it's a common convention.
            // Let's keep logic simple here: apply if set.
            if (!string.IsNullOrWhiteSpace(EnableFor))
            {
                var enableExpr = NormalizeEnableExpression(EnableFor);
                output.Attributes.SetAttribute(":disabled", $"!({enableExpr})");
            }
            else
            {
                var bindDisabledExpr = GetClientBindPath(BindDisabledFor);
                if (string.IsNullOrWhiteSpace(bindDisabledExpr))
                    bindDisabledExpr = BindDisabled;

                if (!string.IsNullOrEmpty(bindDisabledExpr))
                    output.Attributes.SetAttribute(":disabled", bindDisabledExpr);
                else if (Disabled.HasValue)
                    output.Attributes.SetAttribute(":disabled", Disabled.Value.ToString().ToLower());
            }
        }

        protected virtual string NormalizeEnableExpression(string expr)
        {
            var text = (expr ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text)) return "true";

            // Allow Razor-style model paths while keeping concise syntax in .cshtml.
            text = Regex.Replace(text, @"\bModel\.", "");

            // EleForm controls map Item.X to flat form.x (not form.Item.x).
            text = Regex.Replace(text, @"\bItem\.([A-Za-z_][A-Za-z0-9_]*)\b", m =>
            {
                var field = m.Groups[1].Value;
                if (string.IsNullOrEmpty(field)) return "form";
                var camel = char.ToLowerInvariant(field[0]) + field.Substring(1);
                return $"form.{camel}";
            });

            // Support checks like "Item != null" by treating Item as form root.
            text = Regex.Replace(text, @"\bItem\b", "form");

            return text;
        }

        protected string GetClientBindPath(ModelExpression expression)
        {
            if (expression == null || string.IsNullOrWhiteSpace(expression.Name))
                return null;

            var path = expression.Name;
            if (path.StartsWith("Model.", StringComparison.OrdinalIgnoreCase))
                path = path.Substring("Model.".Length);

            return path;
        }
    }
}
