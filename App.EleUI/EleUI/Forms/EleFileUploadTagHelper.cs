using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleFileUpload")]
    public class EleFileUploadTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Exts")]
        public string Exts { get; set; } = ".pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.zip,.rar,.7z";

        [HtmlAttributeName("MaxSizeMb")]
        public int MaxSizeMb { get; set; } = 20;

        [HtmlAttributeName("ButtonText")]
        public string ButtonText { get; set; } = "选择文件";

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            TryAutoSetLabel();

            var vModel = GetVModel(context);
            var propName = GetPropName();
            output.TagName = null;

            if (string.IsNullOrWhiteSpace(vModel) || string.IsNullOrWhiteSpace(propName))
            {
                output.Content.SetHtmlContent(string.Empty);
                await RenderWrapper(output);
                return;
            }

            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var fileInputId = $"fileInput_{propName}_{uniqueId}";
            var maxSizeMb = MaxSizeMb > 0 ? MaxSizeMb : 20;
            var exts = string.IsNullOrWhiteSpace(Exts) ? string.Empty : Exts.Trim();
            var escapedExts = EscapeJs(exts);
            var escapedButton = EscapeJs(ButtonText ?? "选择文件");

            var content = $@"
<div class=""ele-file-upload-client flex items-center gap-2 w-full"">
    <input
        type=""file""
        id=""{fileInputId}""
        style=""display:none""
        accept=""{exts}""
        @change=""(e) => handleClientFileUpload(e, '{propName}', '{escapedExts}', {maxSizeMb})""
    />

    <el-input
        class=""w-full""
        :model-value=""getUploadedFileName({vModel})""
        readonly
        placeholder=""{Placeholder}""
    ></el-input>

    <el-button type=""primary"" plain @click=""triggerFileInput('{fileInputId}')"">{escapedButton}</el-button>
    <el-button v-if=""{vModel}"" @click=""{vModel} = ''"">清空</el-button>
</div>";

            output.Content.SetHtmlContent(content);
            await RenderWrapper(output);
        }
    }
}
