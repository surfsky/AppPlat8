using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using App.Components;
using App.Utils;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text.Json;

namespace App.EleUI
{
    /// <summary>
    /// Element plus form control
    /// </summary>
    public abstract class EleFormControl : EleControl
    {
        [HtmlAttributeName("For")]           public ModelExpression For { get; set; }
        [HtmlAttributeName("Label")]         public string Label { get; set; }
        [HtmlAttributeName("Required")]      public bool Required { get; set; }
        [HtmlAttributeName("LabelWidth")]    public string LabelWidth { get; set; } = "100px";
        [HtmlAttributeName("Clearable")]     public bool? Clearable { get; set; } = true;
        [HtmlAttributeName("Placeholder")]   public string Placeholder { get; set; } = "";
        [HtmlAttributeName("ColSpan")]       public int? ColSpan { get; set; }
        [HtmlAttributeName("FillRow")]       public bool FillRow { get; set; }
        [HtmlAttributeName("Prop")]          public string Prop { get; set; } // Manual prop path,(e.g. form.title)???

        /// <summary>
        /// Default value for the control. In filter context, also rendered as
        /// <c>data-filter-default</c> + <c>data-filter-model</c> so the EleTable
        /// app can pre-populate the corresponding filter (typically from a URL
        /// parameter on the page handler).
        /// </summary>
        [HtmlAttributeName("Value")]         public object Value { get; set; }

        // Get Vue Model Path (e.g. form.title)
        protected string GetVModel(TagHelperContext context)
        {
            // Prefer VModel if set explicitly in base
            // Prefer Prop if set (manual override)
            if (!string.IsNullOrEmpty(VModel)) return VModel;
            if (!string.IsNullOrEmpty(Prop)) return Prop;

            if (For != null)
            {
                var name = For.Name; // e.g. Item.Title
                var propName = name;
                if (propName.Contains("."))
                    propName = propName.Substring(propName.LastIndexOf('.') + 1);
                var camelName = ToCamelCase(propName);
                
                bool isEleForm = context.Items.ContainsKey("IsEleForm");
                if (isEleForm)
                {
                    var formModel = context.Items.ContainsKey("EleFormModel") ? context.Items["EleFormModel"] as string : "form";
                    return $"{formModel}.{camelName}";
                }
                else
                {
                    // Filter context
                    return $"filters.{camelName}";
                }
            }
            return null;
        }

        protected string GetPropName()
        {
             if (For != null)
            {
                var name = For.Name;
                var propName = name;
                if (propName.Contains("."))
                    propName = propName.Substring(propName.LastIndexOf('.') + 1);
                return ToCamelCase(propName);
            }
            return Prop;
        }

        protected string ToCamelCase(string s)
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

        /// <summary>
        /// Get raw string representation of <see cref="Value"/> suitable for a
        /// plain HTML attribute (e.g. <c>data-filter-default</c>).
        /// </summary>
        protected string GetDefaultRaw()
        {
            return Value == null ? null : FormatDefaultRaw(Value);
        }

        /// <summary>
        /// Get JavaScript/Vue expression for <see cref="Value"/> suitable for an
        /// interpolated Vue binding (e.g. <c>:model-value</c>).
        /// </summary>
        protected string GetDefaultValueExpression()
        {
            return Value == null ? null : FormatDefaultValueExpression(Value);
        }

        /// <summary>
        /// Format a value for a plain HTML attribute.
        /// </summary>
        private static string FormatDefaultRaw(object value)
        {
            if (value == null) return null;
            if (value is bool b) return b ? "true" : "false";
            if (value is string s) return s;
            var t = value.GetType();
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t.IsEnum) return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (t.IsPrimitive) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (value is System.Collections.IEnumerable enumerable)
            {
                var parts = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    if (item is bool bi) parts.Add(bi ? "true" : "false");
                    else if (item.GetType().IsPrimitive) parts.Add(Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture));
                    else if (item is string si) parts.Add($"\"{si.Replace("\"", "\\\"")}\"");
                    else parts.Add(Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture));
                }
                return $"[{string.Join(",", parts)}]";
            }
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format a value as a JavaScript/Vue expression.
        /// </summary>
        private static string FormatDefaultValueExpression(object value)
        {
            if (value == null) return null;
            if (value is bool b) return b ? "true" : "false";
            if (value is string s) return $"'{s.Replace("'", "\\'")}'";
            var t = value.GetType();
            t = Nullable.GetUnderlyingType(t) ?? t;
            if (t.IsEnum) return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (t.IsPrimitive) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            if (value is System.Collections.IEnumerable enumerable)
            {
                var parts = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item == null) continue;
                    if (item is bool bi) parts.Add(bi ? "true" : "false");
                    else if (item.GetType().IsPrimitive) parts.Add(Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture));
                    else if (item is string si) parts.Add($"'{si.Replace("'", "\\'")}'");
                    else parts.Add(Convert.ToString(item, System.Globalization.CultureInfo.InvariantCulture));
                }
                return $"[{string.Join(",", parts)}]";
            }
            // Object: serialize as JSON
            return JsonSerializer.Serialize(value);
        }

        /// <summary>
        /// When the control is used inside an EleTable filter row and a
        /// <see cref="Value"/> has been provided, emit
        /// <c>data-filter-default</c> + <c>data-filter-model</c> attributes so
        /// the JS side can pre-populate the filter state and trigger the
        /// initial data load. This is what enables URL-parameter-driven
        /// defaults (e.g. <c>?orgId=730</c>).
        /// </summary>
        protected void TrySetFilterDefault(TagHelperContext context, TagHelperOutput output)
        {
            if (Value == null) return;
            if (context.Items.ContainsKey("IsEleForm")) return; // Only valid in filter context
            if (context.Items.ContainsKey("FilterDefaultSuppressed")) return;

            var vModel = GetVModel(context);
            if (string.IsNullOrEmpty(vModel) || !vModel.StartsWith("filters.", StringComparison.Ordinal))
                return;

            var propName = vModel.Substring("filters.".Length);
            var defaultRaw = GetDefaultRaw();
            if (string.IsNullOrEmpty(defaultRaw)) return;

            output.Attributes.SetAttribute("data-filter-default", defaultRaw);
            if (!string.IsNullOrWhiteSpace(propName))
                output.Attributes.SetAttribute("data-filter-model", propName);
        }

        protected void TryAutoSetLabel()
        {
            if (string.IsNullOrEmpty(Label) && For != null)
            {
                // Try DisplayName first (from DisplayAttribute)
                if (!string.IsNullOrEmpty(For.Metadata.DisplayName))
                    Label = For.Metadata.DisplayName;
                else
                {
                    // Try UIAttribute or DescriptionAttribute manually
                    var propName = For.Metadata.PropertyName;
                    var containerType = For.Metadata.ContainerType;
                    if (containerType != null && !string.IsNullOrEmpty(propName))
                    {
                        var propInfo = containerType.GetProperty(propName);
                        if (propInfo != null)
                        {
                            var uiAttr = propInfo.GetCustomAttribute<UIAttribute>();
                            if (uiAttr != null)
                                Label = uiAttr.Title;
                            else
                            {
                                var descAttr = propInfo.GetCustomAttribute<DescriptionAttribute>();
                                if (descAttr != null)
                                {
                                    Label = descAttr.Description;
                                }
                            }
                        }
                    }
                }
            }
        }

        protected override void AddCommonAttributes(TagHelperContext context, TagHelperOutput output)
        {
            // Call base to handle VModel, explicit Width/Height/Enabled
            base.AddCommonAttributes(context, output);
            if (!string.IsNullOrEmpty(Placeholder))      
                output.Attributes.SetAttribute("placeholder", Placeholder);
            
            TryAutoSetLabel();

            // Set v-model data
            var vModel = GetVModel(context);
            if (!string.IsNullOrEmpty(vModel) && string.IsNullOrEmpty(VModel))
                 output.Attributes.SetAttribute("v-model", vModel);
            
            // Width
            if (string.IsNullOrEmpty(Width))
            {
                // If label is present, we assume it's in a form/column structure, default 100%
                if (!string.IsNullOrEmpty(Label))
                     output.Attributes.SetAttribute("style", "width: 100%");
                else
                    // Filter context default width
                    output.Attributes.SetAttribute("style", "width: 200px");
            }

            // Enable/Disable
            string baseDisabledExpr;
            var enabledForPath = GetBindPath(EnabledFor);
            if (!string.IsNullOrWhiteSpace(enabledForPath))
            {
                var clientPath = ToClientFormPath(enabledForPath);
                baseDisabledExpr = $"!({clientPath})";
            }
            else if (context.AllAttributes.ContainsName("Enabled"))
            {
                baseDisabledExpr = (!Enabled).ToString().ToLower();
            }
            else
            {
                baseDisabledExpr = "readOnly";
            }

            var target = ResolveControlTarget(context);
            if (!string.IsNullOrWhiteSpace(target))
            {
                var safeTarget = EscapeJs(target);
                output.Attributes.SetAttribute(":disabled", $"(typeof resolveControlDisabled === 'function' ? resolveControlDisabled('{safeTarget}', {baseDisabledExpr}) : ({baseDisabledExpr}))");
                output.Attributes.SetAttribute("v-show", $"(typeof resolveControlVisible === 'function' ? resolveControlVisible('{safeTarget}', true) : true)");
            }
            else
            {
                output.Attributes.SetAttribute(":disabled", baseDisabledExpr);
            }

            // Clearable default logic
            bool isClearable = this.Clearable ?? (!context.Items.ContainsKey("IsEleForm"));  //???
            if (isClearable)
                 output.Attributes.SetAttribute("clearable", "true");
        }

        protected override string ResolveControlId(TagHelperContext context)
        {
            if (!string.IsNullOrWhiteSpace(ControlId))
                return ControlId.Trim();
            return ResolveControlTarget(context);
        }

        protected override string ResolveFieldExpress(TagHelperContext context)
        {
            if (!string.IsNullOrWhiteSpace(FieldExpress))
                return FieldExpress.Trim();
            return GetPropName();
        }

        protected string ResolveControlTarget(TagHelperContext context)
        {
            var field = ResolveFieldExpress(context);
            if (!string.IsNullOrWhiteSpace(field))
                return $"field:{field}";

            var controlId = !string.IsNullOrWhiteSpace(ControlId) ? ControlId.Trim() : null;
            if (!string.IsNullOrWhiteSpace(controlId))
                return $"controlId:{controlId}";

            return null;
        }

        protected Task RenderWrapper(TagHelperOutput output)
        {
            // TryAutoSetLabel has been called in AddCommonAttributes, but RenderWrapper might be called later?
            // Actually ProcessAsync calls AddCommonAttributes then RenderWrapper. So Label should be set.
            
            if (!string.IsNullOrEmpty(Label))
            {
                var prop = GetPropName();
                var encodedLabel = WebUtility.HtmlEncode(Label);
                var rulesAttr = "";
                if (Required)
                {
                    var msg = $"{Label}不能为空";
                    rulesAttr = $@":rules=""[{{ required: true, message: '{msg}', trigger: 'blur' }}]""";
                }
                
                var labelWidthAttr = !string.IsNullOrEmpty(LabelWidth) ? $@"label-width=""{LabelWidth}""" : "";
                
                // Column logic
                var classAttr = "";
                if (FillRow)
                {
                     classAttr = @" class=""col-span-full""";
                }
                else if (ColSpan.HasValue)
                {
                     // Simple mapping for 4-col grid
                     if (ColSpan >= 24)      classAttr = @" class=""col-span-full""";
                     else if (ColSpan >= 12) classAttr = @" class=""col-span-1 md:col-span-2 lg:col-span-2""";
                     else if (ColSpan >= 6)  classAttr = @" class=""col-span-1""";
                }

                output.PreElement.SetHtmlContent($@"<el-form-item prop=""{prop}"" {rulesAttr} {labelWidthAttr}{classAttr}>
    <template #label>
        <span class=""block w-full overflow-hidden text-ellipsis whitespace-nowrap"" title=""{encodedLabel}"">{encodedLabel}</span>
    </template>");
                output.PostElement.SetHtmlContent("</el-form-item>");

            }

            return Task.CompletedTask;
        }
    }
}
