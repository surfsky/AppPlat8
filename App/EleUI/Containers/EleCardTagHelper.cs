using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// EleCard 子标签共享上下文。
    /// </summary>
    public class CardContext
    {
        public StringBuilder HeaderHtml { get; set; } = new StringBuilder();
    }

    /// <summary>
    /// 卡片自定义头部 slot。用法：<Header>...</Header>
    /// </summary>
    [HtmlTargetElement("Header", ParentTag = "EleCard")]
    public class EleCardHeaderTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            output.SuppressOutput();
            if (context.Items.TryGetValue(typeof(CardContext), out var obj) && obj is CardContext cardCtx)
            {
                var content = await output.GetChildContentAsync();
                cardCtx.HeaderHtml.Append(content.GetContent());
            }
        }
    }

    /// <summary>
    /// 卡片容器。输出 el-card，并支持通过 Title 快速生成 header slot。
    /// </summary>
    [HtmlTargetElement("EleCard")]
    public class EleCardTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("Title")]
        public string Title { get; set; }

        [HtmlAttributeName("Icon")]
        public string Icon { get; set; }

        [HtmlAttributeName("Collapsable")]
        public bool Collapsable { get; set; } = false;

        [HtmlAttributeName("Collapsible")]
        public bool CollapsibleAlias { get; set; } = false;

        [HtmlAttributeName("BodyStyle")]
        public string BodyStyle { get; set; }

        [HtmlAttributeName("HeaderClass")]
        public string HeaderClass { get; set; } = "card-header";

        public override void Init(TagHelperContext context)
        {
            base.Init(context);
            context.Items[typeof(CardContext)] = new CardContext();
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "el-card";
            AddCommonAttributes(context, output);

            var cardShadow = string.IsNullOrWhiteSpace(Shadow) ? "always" : Shadow.Trim();
            if (cardShadow == "always" || cardShadow == "hover" || cardShadow == "never")
                output.Attributes.SetAttribute("shadow", cardShadow);

            if (!string.IsNullOrWhiteSpace(BodyStyle))
                output.Attributes.SetAttribute("body-style", BodyStyle);

            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();
            var headerTemplate = string.Empty;

            var cardCtx = (CardContext)context.Items[typeof(CardContext)];
            var isCollapsable = Collapsable || CollapsibleAlias;
            var bodyId = $"ele_card_body_{SanitizeId(context.UniqueId)}";
            var arrowId = $"ele_card_arrow_{SanitizeId(context.UniqueId)}";

            if (cardCtx.HeaderHtml.Length > 0)
            {
                // 优先使用 <Header> 子标签提供的自定义 header
                headerTemplate = $"<template #header>{cardCtx.HeaderHtml}</template>";
            }
            else if (!string.IsNullOrWhiteSpace(Title) || !string.IsNullOrWhiteSpace(Icon) || isCollapsable)
            {
                var headerClass = string.IsNullOrWhiteSpace(HeaderClass)
                    ? "flex items-center justify-between"
                    : $"{HeaderClass} flex items-center justify-between";
                var title = string.IsNullOrWhiteSpace(Title) ? "Card" : Title;
                var iconHtml = BuildIconHtml(Icon);
                var leftHtml = $"<div class='flex items-center gap-2'>{iconHtml}<span>{title}</span></div>";
                var rightHtml = isCollapsable
                    ? $"<button type='button' class='inline-flex items-center justify-center text-slate-500 hover:text-slate-700 transition-colors' onclick=\"window.__eleToggleCardBody && window.__eleToggleCardBody('{bodyId}','{arrowId}');return false;\" aria-label='toggle card body'><el-icon id='{arrowId}'><component :is=\"'ArrowDown'\"></component></el-icon></button>"
                    : "";

                headerTemplate = $"<template #header><div class='{headerClass}'>{leftHtml}{rightHtml}</div></template>";
            }

            if (isCollapsable)
            {
                content = $"<div id='{bodyId}'>{content}</div>";
                output.PostElement.AppendHtml(@"<script>
if (!window.__eleToggleCardBody) {
  window.__eleToggleCardBody = function(bodyId, arrowId) {
    var inner = document.getElementById(bodyId);
    var arrow = document.getElementById(arrowId);
    if (!inner) return;
    var cardBody = inner.parentElement;
    if (!cardBody) return;
    var isHidden = cardBody.style.display === 'none';
    cardBody.style.display = isHidden ? '' : 'none';
    if (arrow) {
      arrow.style.transition = 'transform 0.2s ease';
      arrow.style.transform = isHidden ? 'rotate(0deg)' : 'rotate(-90deg)';
    }
  };
}
</script>");
            }

            output.Content.SetHtmlContent(headerTemplate + content);
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "card";

            return value.Replace("-", "_").Replace(":", "_");
        }

        private static string BuildIconHtml(string icon)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return string.Empty;

            var trimmed = icon.Trim();
            var escaped = trimmed.Replace("'", "\\'").Replace("\"", "&quot;");

            if (trimmed.Contains(" ") || trimmed.StartsWith("fa") || trimmed.Contains("icon-"))
                return $"<i class='{escaped}'></i>";

            return $"<el-icon><component :is=\"'{escaped}'\"></component></el-icon>";
        }
    }
}