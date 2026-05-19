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

        [HtmlAttributeName("ViewerUrl")]
        public string ViewerUrl { get; set; } = string.Empty;

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
            var escapedViewerUrl = EscapeJs(ViewerUrl ?? string.Empty);
            var placeholder = EscapeJs(Placeholder ?? string.Empty);
            var target = ResolveControlTarget(context);
            var targetSafe = string.IsNullOrWhiteSpace(target) ? string.Empty : target.Replace("'", "\\'");
            var vVisibleExpr = string.IsNullOrWhiteSpace(target)
                ? "true"
                : $"(typeof resolveControlVisible === 'function' ? resolveControlVisible('{targetSafe}', true) : true)";
            var vDisabledExpr = string.IsNullOrWhiteSpace(target)
                ? "readOnly"
                : $"(typeof resolveControlDisabled === 'function' ? resolveControlDisabled('{targetSafe}', readOnly) : readOnly)";
            var dataControlAttr = string.IsNullOrWhiteSpace(target)
                ? string.Empty
                : $" data-ele-control-id=\"{target}\"";

            var content = $@"
<div class=""ele-file-upload-client flex items-center gap-2 w-full"" v-show=""{vVisibleExpr}""{dataControlAttr}>
    <input
        type=""file""
        id=""{fileInputId}""
        style=""display:none""
        accept=""{exts}""
        :disabled=""{vDisabledExpr}""
        @change=""(e) => handleClientFileUpload(e, '{propName}', '{escapedExts}', {maxSizeMb})""
    />

    <el-button type=""primary"" plain :disabled=""{vDisabledExpr}"" @click=""triggerFileInput('{fileInputId}')"">{escapedButton}</el-button>

    <a
        v-if=""{vModel}""
        href=""javascript:void(0)""
        class=""ele-file-upload-name-link flex-1 min-w-0 truncate text-blue-600 hover:text-blue-700 underline decoration-dotted""
        :title=""getUploadedFileName({vModel})""
        @click=""openFileViewer({vModel}, '{escapedViewerUrl}')""
    >{{{{ getUploadedFileName({vModel}) }}}}</a>

    <span
        v-else
        class=""ele-file-upload-name-placeholder flex-1 min-w-0 truncate text-gray-400""
        title=""{placeholder}""
    >{placeholder}</span>

    <el-button
        v-if=""{vModel}""
        text
        circle
        class=""ele-file-upload-clear-btn""
        :disabled=""{vDisabledExpr}""
        @click=""{vModel} = ''""
        title=""清空""
        aria-label=""清空""
    >✕</el-button>
</div>";

            output.Content.SetHtmlContent(content);
            await RenderWrapper(output);
        }
    }
}
