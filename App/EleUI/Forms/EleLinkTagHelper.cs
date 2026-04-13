using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Net;
using System.Threading.Tasks;

namespace App.EleUI
{
    public enum EleLinkDisplay
    {
        Blank,
        Drawer,
    }

    /// <summary>
    /// 强类型链接控件。
    /// 示例：<Link For="Item.Url" Display="Blank" Text="查看" class="text-xs text-blue-600" />
    /// 示例：<Link For="Item.Url" Display="Drawer" Text="查看" DrawerTitle="附件预览" />
    /// 示例：<Link For="Item.Url" Display="Drawer" Icon="View" Text="" class="text-xs text-blue-600" />
    /// </summary>
    [HtmlTargetElement("Link", Attributes = "For")]
    [HtmlTargetElement("EleLink")]
    public class EleLinkTagHelper : TagHelper
    {
        [HtmlAttributeName("For")]
        public ModelExpression For { get; set; }

        [HtmlAttributeName("Href")]
        public string Href { get; set; }

        [HtmlAttributeName("Display")]
        public EleLinkDisplay Display { get; set; } = EleLinkDisplay.Blank;

        [HtmlAttributeName("Text")]
        public string Text { get; set; } = "";

        [HtmlAttributeName("Icon")]
        public EleIconName Icon { get; set; } = EleIconName.None;

        [HtmlAttributeName("DrawerTitle")]
        public string DrawerTitle { get; set; }

        [HtmlAttributeName("DrawerSize")]
        public string DrawerSize { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "a";
            output.TagMode = TagMode.StartTagAndEndTag;

            var content = Text;
            var textSpecified = context.AllAttributes.ContainsName("Text");
            if (!textSpecified && string.IsNullOrWhiteSpace(content))
            {
                var child = await output.GetChildContentAsync();
                content = child.GetContent();
            }

            var hasText = !string.IsNullOrWhiteSpace(content);
            var hasIcon = Icon != EleIconName.None;
            var drawerTitleText = hasText ? content : "查看";

            if (!hasText && !hasIcon)
            {
                content = "查看";
                hasText = true;
                drawerTitleText = content;
            }

            string contentHtml;
            if (hasIcon && hasText)
            {
                contentHtml = $"<el-icon><component :is=\"'{Icon}'\"></component></el-icon><span class=\"ml-1\">{WebUtility.HtmlEncode(content)}</span>";
            }
            else if (hasIcon)
            {
                contentHtml = $"<el-icon><component :is=\"'{Icon}'\"></component></el-icon>";
            }
            else
            {
                contentHtml = WebUtility.HtmlEncode(content);
            }

            var (hrefValue, isExpression) = ResolveHref();

            if (Display == EleLinkDisplay.Drawer)
            {
                output.Attributes.SetAttribute("href", "javascript:void(0)");

                var drawerTitle = string.IsNullOrWhiteSpace(DrawerTitle) ? drawerTitleText : DrawerTitle;
                var titleArg = $"'{EscapeJs(drawerTitle)}'";
                var sizeArg = string.IsNullOrWhiteSpace(DrawerSize) ? "null" : $"'{EscapeJs(DrawerSize.Trim())}'";
                var urlArg = isExpression ? hrefValue : $"'{EscapeJs(hrefValue)}'";

                output.Attributes.SetAttribute("v-on:click.prevent", $"openLinkInDrawer({urlArg}, {titleArg}, {sizeArg})");
            }
            else
            {
                if (isExpression)
                    output.Attributes.SetAttribute(":href", hrefValue);
                else
                    output.Attributes.SetAttribute("href", hrefValue);

                output.Attributes.SetAttribute("target", "_blank");
                output.Attributes.SetAttribute("rel", "noopener noreferrer");
            }

            output.Attributes.RemoveAll("For");
            output.Attributes.RemoveAll("Display");
            output.Attributes.RemoveAll("Text");
            output.Attributes.RemoveAll("Icon");
            output.Attributes.RemoveAll("icon");
            output.Attributes.RemoveAll("DrawerTitle");
            output.Attributes.RemoveAll("DrawerSize");

            output.Content.SetHtmlContent(contentHtml);
        }

        private (string Value, bool IsExpression) ResolveHref()
        {
            if (For != null && !string.IsNullOrWhiteSpace(For.Name))
                return (BuildClientExpression(For.Name), true);

            if (!string.IsNullOrWhiteSpace(Href))
                return (Href.Trim(), false);

            return ("#", false);
        }

        private static string BuildClientExpression(string path)
        {
            var normalized = path.Trim();

            if (normalized.StartsWith("Model.", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring("Model.".Length);

            if (normalized.StartsWith("Item.", StringComparison.OrdinalIgnoreCase))
                normalized = "item." + normalized.Substring("Item.".Length);

            var parts = normalized.Split('.');
            for (var i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;

                var p = parts[i];
                if (char.IsUpper(p[0]))
                    parts[i] = char.ToLowerInvariant(p[0]) + p.Substring(1);
            }

            return string.Join('.', parts);
        }

        private static string EscapeJs(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
