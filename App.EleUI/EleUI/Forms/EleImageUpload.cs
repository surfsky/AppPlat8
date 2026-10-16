using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleImageUpload")]
    public class EleImageUpload : EleFormControl
    {
        [HtmlAttributeName("Action")]          public string Action { get; set; }
        [HtmlAttributeName("ShowViewer")]      public bool ShowViewer { get; set; } = true;
        [HtmlAttributeName("MaxWidth")]        public int MaxWidth { get; set; } = 1024;
        [HtmlAttributeName("Limit")]           public int Limit { get; set; } = 1;        // 1=单图；>1=限数量多图；<=0=不限数量多图
        [HtmlAttributeName("ItemWidth")]       public int ItemWidth { get; set; } = 200;   // 图片展示宽度
        [HtmlAttributeName("CropWidth")]       public int CropWidth { get; set; } = 1024;  // 图片裁剪宽度

 
        /// <summary>是否多图模式：Limit=1 为单图，其余为多图</summary>
        bool IsMulti => Limit != 1;

        public EleImageUpload() {}

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            // Ensure Label is set (from For or manually)
            TryAutoSetLabel();
            
            // Get property name for binding (e.g. "image")
            var vModel = GetVModel(context); // e.g. "form.image"
            var propName = GetPropName(); // e.g. "image" (camelCase)

            // If we can't determine the model, we can't generate the upload logic correctly
            if (string.IsNullOrEmpty(vModel))
            {
                // Fallback or error? For now, just render nothing or simple upload
            }

            // We are generating a complex structure, so we don't use a single tag
            output.TagName = null;

            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var fileInputId = $"fileInput_{propName}_{uniqueId}";
            var multipleAttr = IsMulti ? "multiple" : "";
            var multiLimitStr = Limit > 0 ? Limit.ToString() : "0";
            var itemWidth = ItemWidth > 0 ? ItemWidth : 200;
            var limitWidth = CropWidth > 0 ? CropWidth : (MaxWidth > 0 ? MaxWidth : 1024);
            var itemImageStyle = $"width:{itemWidth}px; height:{itemWidth}px;";
            var itemBoxStyle = $"width:{itemWidth}px; height:{itemWidth}px;";
            var delImageText = Texts.Current.DeleteImage;
            var target = ResolveControlTarget(context);
            var targetSafe = string.IsNullOrWhiteSpace(target) ? string.Empty : target.Replace("'", "\\'");
            var vVisibleExpr = string.IsNullOrWhiteSpace(target)
                ? "true"
                : $"(typeof resolveControlVisible === 'function' ? resolveControlVisible('{targetSafe}', true) : true)";

            string defaultDisabled;
            var enabledForPath = GetBindPath(EnabledFor);
            if (!string.IsNullOrWhiteSpace(enabledForPath))
            {
                var clientPath = ToClientFormPath(enabledForPath);
                defaultDisabled = $"!({clientPath})";
            }
            else if (context.AllAttributes.ContainsName("Enabled"))
            {
                defaultDisabled = (!Enabled).ToString().ToLower();
            }
            else
            {
                defaultDisabled = context.Items.ContainsKey("IsEleForm") ? "readOnly" : "false";
            }

            var vDisabledExpr = string.IsNullOrWhiteSpace(target)
                ? defaultDisabled
                : $"(typeof resolveControlDisabled === 'function' ? resolveControlDisabled('{targetSafe}', {defaultDisabled}) : {defaultDisabled})";
            var dataControlAttr = string.IsNullOrWhiteSpace(target)
                ? string.Empty
                : $" data-ele-control-id=\"{target}\"";
            
            string content;
            
            if (IsMulti)
            {
                // Multi-file upload mode - displays as grid of images
                content = $@"
            <div class=""ele-image-upload-client-multi"" v-show=""{vVisibleExpr}""{dataControlAttr}>
                <input
                    type=""file""
                    id=""{fileInputId}""
                    style=""display: none""
                    accept=""image/*""
                    {multipleAttr}
                    :disabled=""{vDisabledExpr}""
                    @change=""(e) => handleMultiImageUpload(e, '{propName}', {limitWidth}, {multiLimitStr})""
                />
                <div class=""flex flex-wrap gap-4"">
                    <div v-for=""(img, idx) in getImageList({vModel})"" :key=""idx"" class=""relative inline-block group"">
                        <img :src=""img"" style=""{itemImageStyle}"" class=""block object-contain cursor-pointer rounded border border-gray-200"" @click.stop=""openImageViewerTop(img, getImageList({vModel}), idx)"" />
                        <div v-if=""!({vDisabledExpr})"" class=""absolute -top-2 -right-2 z-10"" @click.stop=""{vModel}.splice(idx, 1)"">
                            <div class=""ele-corner-close-btn"" title=""{delImageText}"">
                                <el-icon class=""ele-corner-close-elicon""><Close /></el-icon>
                            </div>
                        </div>
                    </div>
                    <el-icon v-if=""!({vDisabledExpr}) && ({multiLimitStr} === 0 || getImageList({vModel}).length < {multiLimitStr})"" style=""{itemBoxStyle}"" class=""text-3xl text-gray-400 border border-dashed border-gray-300 rounded cursor-pointer flex justify-center items-center hover:border-blue-400 hover:text-blue-400 transition-colors"" @click=""triggerFileInput('{fileInputId}')"">
                        <Plus/>
                    </el-icon>
                </div>
            </div>";
            }
            else
            {
                // Single file upload mode (original)
                var deleteClick = $"{vModel} = ''";
                content = $@"
            <div class=""ele-image-upload-client"" v-show=""{vVisibleExpr}""{dataControlAttr}>
                <input
                    type=""file""
                    id=""{fileInputId}""
                    style=""display: none""
                    accept=""image/*""
                    :disabled=""{vDisabledExpr}""
                    @change=""(e) => handleClientImageUpload(e, '{propName}', {limitWidth})""
                />
                <div v-if=""{vModel}"" class=""relative inline-block group overflow-visible"">
                    <img :src=""{vModel}"" style=""{itemImageStyle}"" class=""block object-contain cursor-pointer rounded border border-gray-200"" @click.stop=""openImageViewerTop({vModel})"" />
                    <div v-if=""!({vDisabledExpr})"" class=""absolute -top-2 -right-2 z-10"" @click.stop=""{deleteClick}"">
                        <div class=""ele-corner-close-btn"" title=""{delImageText}"">
                            <el-icon class=""ele-corner-close-elicon""><Close /></el-icon>
                        </div>
                    </div>
                </div>
                <el-icon v-else-if=""!({vDisabledExpr})"" style=""{itemBoxStyle}"" class=""text-3xl text-gray-400 border border-dashed border-gray-300 rounded cursor-pointer flex justify-center items-center hover:border-blue-400 hover:text-blue-400 transition-colors"" @click=""triggerFileInput('{fileInputId}')"">
                    <Plus/>
                </el-icon>
            </div>";
            }

            if (ShowViewer)
            {
                content += $@"
            <el-image-viewer v-if=""showViewer"" @close=""showViewer = false"" :url-list=""previewList"" :initial-index=""previewIndex"" :infinite=""false"" :class=""previewList.length <= 1 ? 'ele-image-viewer-single' : ''""/>";
            }

            output.Content.SetHtmlContent(content);

            // This wraps the content in <el-col><el-form-item>...</el-form-item></el-col>
            await RenderWrapper(output);
        }
    }
}
