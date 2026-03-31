using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using App.Utils;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// 表单标签对齐位置，对应 el-form 的 label-position。
    /// </summary>
    public enum EleFormLabelPosition
    {
        Left,
        Right,
        Top,
    }    

    [HtmlTargetElement("EleForm")]
    public class EleFormTagHelper : TagHelper
    {
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        [HtmlAttributeName("Model")]
        public string Model { get; set; } = "form";

        [HtmlAttributeName("LabelWidth")]
        public string LabelWidth { get; set; } = "120px";

        [HtmlAttributeName("LabelPosition")]
        public EleFormLabelPosition LabelPosition { get; set; } = EleFormLabelPosition.Right;

        [HtmlAttributeName("DataHandler")]
        public string DataHandler { get; set; } = "?handler=Data";

        [HtmlAttributeName("SaveHandler")]
        public string SaveHandler { get; set; } = "?handler=Save";

        [HtmlAttributeName("BuildMode")]
        public EleAppBuildMode BuildMode { get; set; } = EleAppBuildMode.Client;

        [HtmlAttributeName("DataFor")]
        public ModelExpression DataFor { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            context.Items["IsEleForm"] = true;
            context.Items["EleFormModel"] = Model;
            output.TagName = "div";
            output.Attributes.SetAttribute("id", "app");  // TODO：有潜在冲突问题
            output.Attributes.SetAttribute("class", "bg-white p-0 h-full overflow-auto"); // Ensure scrolling

            // 获取子内容
            var childContent = await output.GetChildContentAsync();
            var innerHtml = childContent.GetContent();

            // 提取 Bottom 固底容器
            var bottomInfo = ExtractBottom(innerHtml);
            innerHtml = bottomInfo.CleanInnerHtml;
            var bottomHtml = bottomInfo.BottomHtml;
            var hasBottom = bottomHtml != null;

            // 组装最终HTML
            var wrapperClass = GetWrapperClass(hasBottom);
            var formHtml = CreateForm(innerHtml)
                + CreateFooter(bottomHtml, hasBottom)
                ;
            var wrapperHtml = $"<div class=\"{wrapperClass}\">{formHtml}</div>";
            var scriptHtml = CreateScript();
            output.Content.SetHtmlContent(wrapperHtml + scriptHtml);
        }


        /// <summary>提取 Bottom 固底容器 HTML</summary>
        /// <param name="innerHtml">原始HTML内容</param>
        /// <returns>返回清理后的HTML和BottomHTML</returns>
        private (string CleanInnerHtml, string BottomHtml) ExtractBottom(string innerHtml)
        {
            string bottomHtml = null;

            var legacyBottom = ExtractLegacyBottom(innerHtml);
            if (legacyBottom.BottomHtml != null)
            {
                bottomHtml = legacyBottom.BottomHtml;
                innerHtml = legacyBottom.CleanInnerHtml;
            }

            if (bottomHtml == null)
            {
                var markerBottom = ExtractBottomByMarker(innerHtml);
                if (markerBottom.BottomHtml != null)
                {
                    bottomHtml = markerBottom.BottomHtml;
                    innerHtml = markerBottom.CleanInnerHtml;
                }
            }

            return (innerHtml, bottomHtml);
        }

        /// <summary>提取传统 Bottom HTML</summary>
        private (string CleanInnerHtml, string BottomHtml) ExtractLegacyBottom(string innerHtml)
        {
            var lowerInner = innerHtml.ToLower();
            var startTag = "<bottom";
            var endTag = "</bottom>";
            var startIdx = lowerInner.IndexOf(startTag);
            if (startIdx < 0)
                return (innerHtml, null);

            var endIdx = lowerInner.IndexOf(endTag, startIdx);
            if (endIdx <= startIdx)
                return (innerHtml, null);

            var len = endIdx + endTag.Length - startIdx;
            var bottomHtml = innerHtml.Substring(startIdx, len);
            var cleanInnerHtml = innerHtml.Remove(startIdx, len);
            return (cleanInnerHtml, bottomHtml);
        }

        /// <summary>提取标记 Bottom HTML</summary>
        private (string CleanInnerHtml, string BottomHtml) ExtractBottomByMarker(string innerHtml)
        {
            var markerPattern = new Regex(@"<div[^>]*data-ele-bottom\s*=\s*\""true\""[^>]*>.*?</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var markerMatch = markerPattern.Match(innerHtml);
            if (!markerMatch.Success)
                return (innerHtml, null);

            var bottomHtml = markerMatch.Value;
            var cleanInnerHtml = innerHtml.Remove(markerMatch.Index, markerMatch.Length);

            return (cleanInnerHtml, bottomHtml);
        }

        /// <summary>创建表单HTML</summary>
        private string CreateForm(string innerHtml)
        {
            var formHtml = $"<el-form :model=\"{Model}\" label-width=\"{LabelWidth}\" label-position=\"{GetLabelPositionValue()}\" class=\"w-full px-4 pt-4 grid gap-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-4\" ref=\"formRef\" status-icon scroll-to-error>\n";
            formHtml += innerHtml + "\n";
            formHtml += "</el-form>\n";
            return formHtml;
        }

        private string GetLabelPositionValue()
        {
            return LabelPosition.ToString().ToLowerInvariant();
        }

        /// <summary>创建表单页脚HTML</summary>
        private string CreateFooter(string bottomHtml, bool hasBottom)
        {
            if (!hasBottom)
                return string.Empty;

            var customFooterHtml = "<div class=\"px-4 pb-3 fixed bottom-0 left-0 bg-white w-full border-t border-gray-100 py-2 z-10 flex justify-center shadow-sm\">";
            customFooterHtml += bottomHtml;
            customFooterHtml += "</div>\n";
            return customFooterHtml;
        }

        /// <summary>获取包装器的CSS类</summary>
        private string GetWrapperClass(bool hasBottom)
        {
            return hasBottom ? "pb-[60px]" : string.Empty;
        }

        /// <summary>创建脚本HTML</summary>
        private string CreateScript()
        {
            if (BuildMode == EleAppBuildMode.None)
                return string.Empty;

            if (BuildMode == EleAppBuildMode.Server)
            {
                var initData = ResolveInitDataJson();

                initData = initData.Replace("</script", "<\\/script", StringComparison.OrdinalIgnoreCase);

                return $@"
<script>
    document.addEventListener('DOMContentLoaded', function() {{
        const initData = {initData};
        new EleFormAppBuilder().mount('#app', {{
            dataHandler: '{DataHandler}',
            saveHandler: '{SaveHandler}',
            initData: initData,
            autoLoad: false
        }});
    }});
</script>
";
            }

            return $@"
<script>
    document.addEventListener('DOMContentLoaded', function() {{
        new EleFormAppBuilder().mount('#app', {{
            dataHandler: '{DataHandler}',
            saveHandler: '{SaveHandler}'
        }});
    }});
</script>
";
        }

        private string ResolveInitDataJson()
        {
            if (DataFor != null)
            {
                var value = DataFor.Model;
                if (value == null)
                    return "{}";

                if (value is string json)
                    return string.IsNullOrWhiteSpace(json) ? "{}" : json;

                return value.ToJson();
            }

            return "{}";
        }
    }
}
