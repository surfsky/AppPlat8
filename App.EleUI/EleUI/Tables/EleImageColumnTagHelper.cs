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
    /// <summary>表格图片列，显示缩略图，点击可预览</summary> 
    [HtmlTargetElement("EleImageColumn", ParentTag = "Columns")] 
    public class EleImageColumnTagHelper : EleColumnTagHelper
    {
        [HtmlAttributeName("ThumbnailWidth")]
        public int ThumbnailWidth { get; set; } = 40;

        [HtmlAttributeName("ThumbnailHeight")]
        public int ThumbnailHeight { get; set; } = 40;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            this.Sortable = false;
            
            // Call base to setup attributes (prop, label, width, etc)
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

            // Override content with image template.
            // Click invokes EleManager.openImageViewer for centralized preview behavior.
            
            output.Content.SetHtmlContent($@"
                <template #default=""scope"">
                    <el-image
                        v-if=""scope.row.{propName}""
                        style=""width: {ThumbnailWidth}px; height: {ThumbnailHeight}px; border-radius: 4px; cursor: pointer;""
                        :src=""scope.row.{propName} + '?w={ThumbnailWidth}'""
                        fit=""cover""
                        @click=""openImagePreview(scope.row.{propName}, 0)""
                    />
                </template>
            ");
        }
    }
}
