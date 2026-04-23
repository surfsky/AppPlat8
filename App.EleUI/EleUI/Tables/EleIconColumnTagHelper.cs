using App.Components;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>表格图标列，用于显示 Element Plus 字体图标（如 el-icon-xxx）。</summary>
    [HtmlTargetElement("EleIconColumn", ParentTag = "Columns")]
    public class EleIconColumnTagHelper : EleColumnTagHelper
    {
        /// <summary>图标外层 &lt;i&gt; 的额外 class，如 text-lg text-primary-600。</summary>
        [HtmlAttributeName("IconClass")]
        public string IconClass { get; set; } = "text-lg text-primary-600";

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            await base.ProcessAsync(context, output);

            string propName = Prop;
            if (string.IsNullOrEmpty(propName) && For != null)
            {
                propName = For.Metadata.PropertyName ?? For.Name;
                if (propName.Contains("."))
                    propName = propName.Substring(propName.LastIndexOf('.') + 1);

                if (!string.IsNullOrEmpty(propName) && char.IsUpper(propName[0]))
                {
                    propName = char.ToLower(propName[0]) + propName.Substring(1);
                }
            }

            var iconClass = string.IsNullOrEmpty(IconClass) ? "" : " " + IconClass.Trim();
            output.Content.SetHtmlContent($@"
                <template #default=""scope"">
                    <i v-if=""scope.row.{propName}"" :class=""scope.row.{propName} + '{iconClass}'""></i>
                </template>
            ");
        }
    }
}
