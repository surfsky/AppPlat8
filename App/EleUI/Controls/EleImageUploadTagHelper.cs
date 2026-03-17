using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleImageUpload")]
    public class EleImageUploadTagHelper : EleFormControlTagHelper
    {
        public EleImageUploadTagHelper() {}

        [HtmlAttributeName("Action")]
        public string Action { get; set; }

        [HtmlAttributeName("ShowViewer")]
        public bool ShowViewer { get; set; } = true;

        [HtmlAttributeName("MaxWidth")]
        public int MaxWidth { get; set; } = 1024;

        [HtmlAttributeName("Multi")]
        public bool Multi { get; set; } = false;

        [HtmlAttributeName("MultiLimit")]
        public int MultiLimit { get; set; } = 0; // 0 means unlimited

        [HtmlAttributeName("ItemWidth")]
        public int ItemWidth { get; set; } = 200;

        [HtmlAttributeName("LimitWidth")]
        public int LimitWidth { get; set; } = 1024;

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
            var multipleAttr = Multi ? "multiple" : "";
            var multiLimitStr = MultiLimit > 0 ? MultiLimit.ToString() : "0";
            var itemWidth = ItemWidth > 0 ? ItemWidth : 200;
            var limitWidth = LimitWidth > 0 ? LimitWidth : (MaxWidth > 0 ? MaxWidth : 1024);
            var itemImageStyle = $"width:{itemWidth}px; height:{itemWidth}px;";
            var itemBoxStyle = $"width:{itemWidth}px; height:{itemWidth}px;";
            
            string content;
            
            if (Multi)
            {
                // Multi-file upload mode - displays as grid of images
                content = $@"
            <div class=""ele-image-upload-client-multi"">
                <input
                    type=""file""
                    id=""{fileInputId}""
                    style=""display: none""
                    accept=""image/*""
                    {multipleAttr}
                    @change=""(e) => handleMultiImageUpload(e, '{propName}', {limitWidth}, {multiLimitStr})""
                />
                <div class=""flex flex-wrap gap-4"">
                    <div v-for=""(img, idx) in getImageList({vModel})"" :key=""idx"" class=""relative inline-block group"">
                        <img :src=""img"" style=""{itemImageStyle}"" class=""block object-contain cursor-pointer rounded border border-gray-200"" @click.stop=""handlePreview(img, getImageList({vModel}), idx)"" />
                        <div class=""absolute -top-2 -right-2 z-10"" @click.stop=""{vModel}.splice(idx, 1)"">
                            <div class=""ele-corner-close-btn"" title=""删除图片"">
                                <el-icon class=""ele-corner-close-elicon""><Close /></el-icon>
                            </div>
                        </div>
                    </div>
                    <el-icon v-if=""{multiLimitStr} === 0 || getImageList({vModel}).length < {multiLimitStr}"" style=""{itemBoxStyle}"" class=""text-3xl text-gray-400 border border-dashed border-gray-300 rounded cursor-pointer flex justify-center items-center hover:border-blue-400 hover:text-blue-400 transition-colors"" @click=""triggerFileInput('{fileInputId}')"">
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
            <div class=""ele-image-upload-client"">
                <input
                    type=""file""
                    id=""{fileInputId}""
                    style=""display: none""
                    accept=""image/*""
                    @change=""(e) => handleClientImageUpload(e, '{propName}', {limitWidth})""
                />
                <div v-if=""{vModel}"" class=""relative inline-block group overflow-visible"">
                    <img :src=""{vModel}"" style=""{itemImageStyle}"" class=""block object-contain cursor-pointer rounded border border-gray-200"" @click.stop=""handlePreview({vModel})"" />
                    <div class=""absolute -top-2 -right-2 z-10"" @click.stop=""{deleteClick}"">
                        <div class=""ele-corner-close-btn"" title=""删除图片"">
                            <el-icon class=""ele-corner-close-elicon""><Close /></el-icon>
                        </div>
                    </div>
                </div>
                <el-icon v-else style=""{itemBoxStyle}"" class=""text-3xl text-gray-400 border border-dashed border-gray-300 rounded cursor-pointer flex justify-center items-center hover:border-blue-400 hover:text-blue-400 transition-colors"" @click=""triggerFileInput('{fileInputId}')"">
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
