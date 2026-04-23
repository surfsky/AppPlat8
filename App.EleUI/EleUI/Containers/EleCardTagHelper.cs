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
        public EleIconName Icon { get; set; } = EleIconName.None;

        [HtmlAttributeName("IconCls")]
        public string IconCls { get; set; }

        [HtmlAttributeName("Collapsible")]
        public bool Collapsible { get; set; } = false;

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
            var isCollapsible = Collapsible;
            var bodyId = $"ele_card_body_{SanitizeId(context.UniqueId)}";
            var arrowId = $"ele_card_arrow_{SanitizeId(context.UniqueId)}";

            if (cardCtx.HeaderHtml.Length > 0)
            {
                // 优先使用 <Header> 子标签提供的自定义 header
                headerTemplate = $"<template #header>{cardCtx.HeaderHtml}</template>";
            }
            else if (!string.IsNullOrWhiteSpace(Title) || Icon != EleIconName.None || !string.IsNullOrWhiteSpace(IconCls) || isCollapsible)
            {
                var headerClass = string.IsNullOrWhiteSpace(HeaderClass)
                    ? "flex items-center justify-between"
                    : $"{HeaderClass} flex items-center justify-between";
                var title = string.IsNullOrWhiteSpace(Title) ? "Card" : Title;
                var iconHtml = BuildIconHtml(Icon, IconCls);
                var leftHtml = $"<div class='flex items-center gap-2'>{iconHtml}<span>{title}</span></div>";
                var rightHtml = isCollapsible
                    ? $"<button type='button' class='inline-flex items-center justify-center text-slate-500 hover:text-slate-700 transition-colors' onclick=\"window.__eleToggleCardBody && window.__eleToggleCardBody('{bodyId}','{arrowId}');return false;\" aria-label='toggle card body'><el-icon id='{arrowId}'><component :is=\"'ArrowDown'\"></component></el-icon></button>"
                    : "";

                headerTemplate = $"<template #header><div class='{headerClass}'>{leftHtml}{rightHtml}</div></template>";
            }

            if (isCollapsible)
            {
                content = $"<div id='{bodyId}' class='ele-card-collapse-body' data-collapsed='false'>{content}</div>";
                output.PostElement.AppendHtml(@"<script>
if (!window.__eleToggleCardBody) {
    window.__eleInitCardBody = function(bodyId) {
        var inner = document.getElementById(bodyId);
        if (!inner || inner.dataset.eleCardInited === 'true') return;
        var cardBody = inner.parentElement;
        if (!cardBody) return;

        inner.dataset.eleCardInited = 'true';
        cardBody.dataset.eleCardInited = 'true';
        cardBody.dataset.collapsed = 'false';

        var bodyStyle = window.getComputedStyle(cardBody);
        cardBody.dataset.originPaddingTop = bodyStyle.paddingTop || '20px';
        cardBody.dataset.originPaddingBottom = bodyStyle.paddingBottom || '20px';

        cardBody.style.overflow = 'hidden';
        cardBody.style.opacity = '1';
        cardBody.style.maxHeight = 'none';
        cardBody.style.transition = 'max-height 0.25s ease, padding-top 0.25s ease, padding-bottom 0.25s ease, opacity 0.2s ease';
    };

    window.__eleToggleCardBody = function(bodyId, arrowId) {
        var inner = document.getElementById(bodyId);
        var arrow = document.getElementById(arrowId);
        if (!inner) return;
        window.__eleInitCardBody(bodyId);
        var cardBody = inner.parentElement;
        if (!cardBody) return;

        var originPaddingTop = cardBody.dataset.originPaddingTop || '20px';
        var originPaddingBottom = cardBody.dataset.originPaddingBottom || '20px';
        var isCollapsed = cardBody.dataset.collapsed === 'true';
        var durationMs = 260;

        if (cardBody.__eleAnimTimer) {
            clearTimeout(cardBody.__eleAnimTimer);
            cardBody.__eleAnimTimer = null;
        }

        if (isCollapsed) {
            cardBody.style.display = '';
            var fullHeight = inner.scrollHeight +
                parseFloat(originPaddingTop || '0') +
                parseFloat(originPaddingBottom || '0');

            cardBody.style.transition = 'none';
            cardBody.style.maxHeight = '0px';
            cardBody.style.paddingTop = '0px';
            cardBody.style.paddingBottom = '0px';
            cardBody.style.opacity = '0';
            cardBody.offsetHeight;

            window.requestAnimationFrame(function() {
                window.requestAnimationFrame(function() {
                    cardBody.style.transition = 'max-height 0.25s ease, padding-top 0.25s ease, padding-bottom 0.25s ease, opacity 0.2s ease';
                    cardBody.style.maxHeight = fullHeight + 'px';
                    cardBody.style.paddingTop = originPaddingTop;
                    cardBody.style.paddingBottom = originPaddingBottom;
                    cardBody.style.opacity = '1';
                });
            });

            cardBody.dataset.collapsed = 'false';
            cardBody.__eleAnimTimer = setTimeout(function() {
                if (cardBody.dataset.collapsed === 'false') {
                    cardBody.style.maxHeight = 'none';
                }
                cardBody.__eleAnimTimer = null;
            }, durationMs);
        } else {
            cardBody.style.display = '';
            cardBody.style.transition = 'none';
            cardBody.style.maxHeight = cardBody.scrollHeight + 'px';
            cardBody.style.paddingTop = originPaddingTop;
            cardBody.style.paddingBottom = originPaddingBottom;
            cardBody.style.opacity = '1';
            cardBody.offsetHeight;

            window.requestAnimationFrame(function() {
                window.requestAnimationFrame(function() {
                    cardBody.style.transition = 'max-height 0.25s ease, padding-top 0.25s ease, padding-bottom 0.25s ease, opacity 0.2s ease';
                    cardBody.style.maxHeight = '0px';
                    cardBody.style.paddingTop = '0px';
                    cardBody.style.paddingBottom = '0px';
                    cardBody.style.opacity = '0';
                });
            });

            cardBody.dataset.collapsed = 'true';
            cardBody.__eleAnimTimer = setTimeout(function() {
                if (cardBody.dataset.collapsed === 'true') {
                    cardBody.style.display = 'none';
                }
                cardBody.__eleAnimTimer = null;
            }, durationMs);
        }

        if (arrow) {
            arrow.style.transition = 'transform 0.2s ease';
            arrow.style.transform = isCollapsed ? 'rotate(0deg)' : 'rotate(-90deg)';
        }
    };
}

window.__eleInitCardBody && window.__eleInitCardBody('__BODY_ID__');
</script>".Replace("__BODY_ID__", bodyId));
            }

            output.Content.SetHtmlContent(headerTemplate + content);
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "card";

            return value.Replace("-", "_").Replace(":", "_");
        }

        private static string BuildIconHtml(EleIconName icon, string iconCls)
        {
            if (!string.IsNullOrWhiteSpace(iconCls))
            {
                var escapedClass = iconCls.Trim().Replace("'", "\\'").Replace("\"", "&quot;");
                return $"<i class='{escapedClass}'></i>";
            }

            if (icon == EleIconName.None)
                return string.Empty;

            var escaped = icon.ToString().Replace("'", "\\'").Replace("\"", "&quot;");

            if (escaped.Contains(" ") || escaped.StartsWith("fa") || escaped.Contains("icon-"))
                return $"<i class='{escaped}'></i>";

            return $"<el-icon><component :is=\"'{escaped}'\"></component></el-icon>";
        }
    }
}