using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace App.EleUI
{
    [HtmlTargetElement("EleForm")]
    //[RestrictChildren("EleInput", "EleSelect", "EleTreeSelect", "EleDatePicker", "EleSwitch", "EleNumber", "EleUpload", "EleButton", "EleUserSelect", "EleSelector", "EleRadio", "el-form-item", "div", "span", "p", "el-col", "el-row", "EleDrawer", "EleImageUpload", "EleLabel")]
    public class EleFormTagHelper : TagHelper
    {
        [HtmlAttributeName("Model")]
        public string Model { get; set; } = "form";

        [HtmlAttributeName("LabelWidth")]
        public string LabelWidth { get; set; } = "120px";

        [HtmlAttributeName("DataHandler")]
        public string DataHandler { get; set; } = "?handler=Data";

        [HtmlAttributeName("SaveHandler")]
        public string SaveHandler { get; set; } = "?handler=Save";

        [HtmlAttributeName("BuildApp")]
        public bool BuildApp { get; set; } = true;

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

            // 提取工具栏
            var toolbarInfo = ExtractToolbar(innerHtml);
            innerHtml = toolbarInfo.CleanInnerHtml;
            var toolbarHtml = toolbarInfo.ToolbarHtml;
            var toolbarPosition = toolbarInfo.ToolbarPosition;
            context.Items["HasToolbar"] = toolbarHtml != null;
            var hasToolbar = context.Items.ContainsKey("HasToolbar") && (bool)context.Items["HasToolbar"];  //？？？

            // 组装最终HTML
            var wrapperClass = GetWrapperClass(hasToolbar, toolbarPosition);
            var formHtml = CreateToolbar(toolbarHtml, toolbarPosition, hasToolbar)
                + CreateForm(innerHtml)
                + CreateDrawer()
                + CreateFooter(toolbarHtml, toolbarPosition, hasToolbar)
                ;
            var wrapperHtml = $"<div class=\"{wrapperClass}\">{formHtml}</div>";
            var scriptHtml = this.BuildApp ? CreateScript() : "";
            output.Content.SetHtmlContent(wrapperHtml + scriptHtml);
        }


        /// <summary>提取工具栏HTML和位置</summary>
        /// <param name="innerHtml">原始HTML内容</param>
        /// <returns>返回清理后的HTML、工具栏HTML和工具栏位置</returns>
        private (string CleanInnerHtml, string ToolbarHtml, string ToolbarPosition) ExtractToolbar(string innerHtml)
        {
            string toolbarHtml = null;
            var toolbarPosition = "top";

            var legacyToolbar = ExtractLegacyToolbar(innerHtml);
            if (legacyToolbar.ToolbarHtml != null)
            {
                toolbarHtml = legacyToolbar.ToolbarHtml;
                innerHtml = legacyToolbar.CleanInnerHtml;
            }

            if (toolbarHtml == null)
            {
                var markerToolbar = ExtractToolbarByMarker(innerHtml);
                if (markerToolbar.ToolbarHtml != null)
                {
                    toolbarHtml = markerToolbar.ToolbarHtml;
                    innerHtml = markerToolbar.CleanInnerHtml;
                    toolbarPosition = markerToolbar.ToolbarPosition;
                }
            }

            if (toolbarHtml != null && toolbarPosition == "top")
            {
                var posMatchLegacy = Regex.Match(toolbarHtml, @"\bposition\s*=\s*\""(?<p>top|bottom)\""", RegexOptions.IgnoreCase);
                if (posMatchLegacy.Success)
                    toolbarPosition = posMatchLegacy.Groups["p"].Value.ToLower();
            }

            return (innerHtml, toolbarHtml, toolbarPosition);
        }

        /// <summary>提取传统工具栏HTML</summary>
        private (string CleanInnerHtml, string ToolbarHtml) ExtractLegacyToolbar(string innerHtml)
        {
            var lowerInner = innerHtml.ToLower();
            var startTag = "<toolbar";
            var endTag = "</toolbar>";
            var startIdx = lowerInner.IndexOf(startTag);
            if (startIdx < 0)
                return (innerHtml, null);

            var endIdx = lowerInner.IndexOf(endTag, startIdx);
            if (endIdx <= startIdx)
                return (innerHtml, null);

            var len = endIdx + endTag.Length - startIdx;
            var toolbarHtml = innerHtml.Substring(startIdx, len);
            var cleanInnerHtml = innerHtml.Remove(startIdx, len);
            return (cleanInnerHtml, toolbarHtml);
        }

        /// <summary>提取标记工具栏HTML和位置</summary>
        private (string CleanInnerHtml, string ToolbarHtml, string ToolbarPosition) ExtractToolbarByMarker(string innerHtml)
        {
            var markerPattern = new Regex(@"<div[^>]*data-ele-toolbar\s*=\s*\""true\""[^>]*>.*?</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            var markerMatch = markerPattern.Match(innerHtml);
            if (!markerMatch.Success)
                return (innerHtml, null, "top");

            var toolbarHtml = markerMatch.Value;
            var cleanInnerHtml = innerHtml.Remove(markerMatch.Index, markerMatch.Length);
            var toolbarPosition = "top";

            var posMatch = Regex.Match(toolbarHtml, @"data-toolbar-position\s*=\s*\""(?<p>top|bottom)\""", RegexOptions.IgnoreCase);
            if (posMatch.Success)
                toolbarPosition = posMatch.Groups["p"].Value.ToLower();

            return (cleanInnerHtml, toolbarHtml, toolbarPosition);
        }

        /// <summary>创建工具栏HTML</summary>
        private string CreateToolbar(string toolbarHtml, string toolbarPosition, bool hasToolbar)
        {
            if (!hasToolbar || toolbarPosition != "top")
                return string.Empty;

            return $"<div class=\"fixed top-0 left-0 bg-white w-full border-b border-gray-100 py-2 px-4 z-10 shadow-sm\">{toolbarHtml}</div>\n";
        }

        /// <summary>创建表单HTML</summary>
        private string CreateForm(string innerHtml)
        {
            var formHtml = $"<el-form :model=\"{Model}\" label-width=\"{LabelWidth}\" class=\"w-full px-4 pt-4 grid gap-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-4\" ref=\"formRef\" status-icon scroll-to-error>\n";
            formHtml += innerHtml + "\n";
            formHtml += "</el-form>\n";
            return formHtml;
        }

        /// <summary>创建选择器抽屉HTML</summary>
        private string CreateDrawer()
        {
            var drawerHtml = "<el-drawer v-model=\"selectorVisible\" :title=\"selectorTitle\" direction=\"rtl\" :size=\"selectorDrawerSize\" :with-header=\"true\" append-to-body :destroy-on-close=\"true\" class=\"ele-drawer-iframe\">";
            drawerHtml += "<div class=\"h-full overflow-hidden\">";
            drawerHtml += "<iframe v-if=\"selectorVisible\" :src=\"selectorUrl\" style=\"width: 100%; height: 100%; border: 0;\"></iframe>";
            drawerHtml += "</div></el-drawer>\n";
            return drawerHtml;
        }

        /// <summary>创建表单页脚HTML</summary>
        private string CreateFooter(string toolbarHtml, string toolbarPosition, bool hasToolbar)
        {
            if (!hasToolbar)
            {
                var footerHtml = "<div class=\"px-4 pb-3 fixed bottom-0 left-0 bg-white w-full border-t border-gray-100 py-2 z-10 flex justify-center shadow-sm\">";
                footerHtml += "<div class=\"flex items-center space-x-2\">";
                footerHtml += "<el-button type=\"primary\" v-on:click=\"save\" :loading=\"saving\" v-if=\"!readOnly\">保存</el-button>";
                footerHtml += "<el-button v-on:click=\"close\">关闭</el-button>";
                footerHtml += "<span class=\"text-red-500 text-sm ml-4\" v-if=\"error\">{{ error }}</span>";
                footerHtml += "<span class=\"text-green-600 text-sm ml-2\" v-if=\"success\">{{ success }}</span>";
                footerHtml += "</div></div>\n";
                return footerHtml;
            }

            if (toolbarPosition == "bottom")
            {
                var footerToolbarHtml = "<div class=\"px-4 pb-3 fixed bottom-0 left-0 bg-white w-full border-t border-gray-100 py-2 z-10 flex justify-center shadow-sm\">";
                footerToolbarHtml += toolbarHtml;
                footerToolbarHtml += "</div>\n";
                return footerToolbarHtml;
            }

            return string.Empty;
        }

        /// <summary>获取包装器的CSS类</summary>
        private string GetWrapperClass(bool hasToolbar, string toolbarPosition)
        {
            if (!hasToolbar)
                return "pb-[60px]";

            if (toolbarPosition == "top")
                return "pt-[60px]";

            return "pb-[60px]";
        }

        /// <summary>创建脚本HTML</summary>
        private string CreateScript()
        {
            return $@"
<script>
    document.addEventListener('DOMContentLoaded', function() {{
        var mixins = [];
        if (typeof userSelectMixin !== 'undefined') mixins.push(userSelectMixin);
        if (typeof pageMixin !== 'undefined')       mixins.push(pageMixin);

        new EleFormAppBuilder().mount('#app', {{
            dataHandler: '{DataHandler}',
            saveHandler: '{SaveHandler}',
            mixins: mixins
        }});
    }});
</script>
";
        }
    }
}
