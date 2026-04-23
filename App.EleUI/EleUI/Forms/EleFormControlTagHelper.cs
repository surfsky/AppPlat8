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

namespace App.EleUI
{
    /// <summary>
    /// Element plus form control
    /// </summary>
    public abstract class EleFormControlTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("For")]
        public ModelExpression For { get; set; }

        [HtmlAttributeName("Label")]
        public string Label { get; set; }

        [HtmlAttributeName("Required")]
        public bool Required { get; set; }

        [HtmlAttributeName("LabelWidth")]
        public string LabelWidth { get; set; } = "100px";

        [HtmlAttributeName("Clearable")]
        public bool? Clearable { get; set; } = true;

        [HtmlAttributeName("Placeholder")]
        public string Placeholder { get; set; } = "";

        /// <summary>Column span for form item？？？</summary>
        [HtmlAttributeName("ColSpan")]
        public int? ColSpan { get; set; }

        [HtmlAttributeName("FillRow")]
        public bool FillRow { get; set; }

        /// <summary>Manual prop path (e.g. form.title)???</summary>
        [HtmlAttributeName("Prop")]
        public string Prop { get; set; } // Manual prop path

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
            if (string.IsNullOrEmpty(s) || !char.IsUpper(s[0])) return s;
            return char.ToLower(s[0]) + s.Substring(1);
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

        protected async Task RenderWrapper(TagHelperOutput output)
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
        }
    }
}
