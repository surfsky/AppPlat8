using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// Renders a small bootstrap script for Vue + Element Plus and mounts to a target element.
    /// Usage: <EleApp />
    /// </summary>
    [HtmlTargetElement("EleApp")]
    public class EleAppTagHelper : TagHelper
    {
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        /// <summary>Vue app mount point, default is "#app"</summary>
        [HtmlAttributeName("Mount")]
        public string Mount { get; set; } = "body";

        /// <summary>Whether to use Chinese locale for Element Plus</summary>
        [HtmlAttributeName("UseLocale")]
        public bool UseLocale { get; set; } = true;

        /// <summary>Whether to register Element Plus icons</summary>
        [HtmlAttributeName("RegisterIcons")]
        public bool RegisterIcons { get; set; } = true;

        /// <summary>
        /// Optional global object name to expose as Vue setup return.
        /// Example: Expose="eleManagerDemo" and define window.eleManagerDemo = { showSuccess() { ... } }.
        /// </summary>
        [HtmlAttributeName("Expose")]
        public string Expose { get; set; }

        /// <summary>Processes the tag helper.</summary>
        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = null;
            var exposeName = string.IsNullOrWhiteSpace(Expose) ? "model" : EscapeJs(Expose);
            var exposeCode = $"'{exposeName}'";
            var mountCode = EscapeJs(Mount);
            var autoExposeModelJson = BuildAutoExposeModelJson();

            output.Content.SetHtmlContent($@"
<script>
    (() => {{
        const exposeName = {exposeCode};
        const autoExposeModel = {autoExposeModelJson};
        if (autoExposeModel && typeof autoExposeModel === 'object') {{
            const currentExpose = window[exposeName];
            if (currentExpose && typeof currentExpose === 'object') {{
                window[exposeName] = {{ ...autoExposeModel, ...currentExpose }};
            }} else {{
                window[exposeName] = autoExposeModel;
            }}
        }}

        const mountApp = (retry = 0) => {{
            if (!window.EleAppBuilder) {{
                if (retry < 40) {{
                    setTimeout(() => mountApp(retry + 1), 25);
                    return;
                }}
                console.error('EleAppBuilder is not available. Ensure /res/App.EleUI.EleUIJs.EleUI.js is loaded.');
                return;
            }}

            new window.EleAppBuilder().mount('{mountCode}', {{
                useLocale: {(UseLocale ? "true" : "false")},
                registerIcons: {(RegisterIcons ? "true" : "false")},
                exposeName: exposeName
            }});
        }};

        if (document.readyState === 'loading') {{
            document.addEventListener('DOMContentLoaded', () => mountApp(), {{ once: true }});
        }} else {{
            mountApp();
        }}
    }})();
</script>");
        }

        private string BuildAutoExposeModelJson()
        {
            var pageModel = ViewContext?.ViewData?.Model;
            if (pageModel == null)
                return "{}";

            var modelData = new Dictionary<string, object>();
            var properties = pageModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;
                if (property.GetCustomAttribute<BindPropertyAttribute>() == null)
                    continue;

                var rawType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (!IsSupportedBindPropertyType(rawType))
                    continue;

                modelData[property.Name] = property.GetValue(pageModel);
            }

            return JsonSerializer.Serialize(modelData);
        }

        private static bool IsSupportedBindPropertyType(Type type)
        {
            if (type.IsEnum)
                return true;
            if (type == typeof(Guid))
                return true;

            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Boolean:
                case TypeCode.Char:
                case TypeCode.String:
                case TypeCode.DateTime:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        private static string EscapeJs(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
        }
    }
}
