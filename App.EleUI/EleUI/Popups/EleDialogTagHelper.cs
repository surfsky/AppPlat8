using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// 对话框上下文，供 Header/Content/Footer 子标签共享。
    /// </summary>
    public class DialogContext
    {
        public StringBuilder HeaderHtml { get; set; } = new StringBuilder();
        public StringBuilder ContentHtml { get; set; } = new StringBuilder();
        public StringBuilder FooterHtml { get; set; } = new StringBuilder();
        public DialogFooterAlign FooterAlign { get; set; } = DialogFooterAlign.End;
        public string FooterHeight { get; set; }
    }

    /// <summary>
    /// 对话框底部工具栏对齐方式。
    /// </summary>
    public enum DialogFooterAlign
    {
        Start,
        Center,
        End,
        SpaceBetween
    }

    /// <summary>
    /// 对话框组件。输出 el-dialog。
    /// </summary>
    [HtmlTargetElement("EleDialog")]
    public class EleDialogTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("Title")]
        public string Title { get; set; }

        [HtmlAttributeName("Width")]
        public string DialogWidth { get; set; } = "50%";

        [HtmlAttributeName("Top")]
        public string Top { get; set; }

        [HtmlAttributeName("AppendToBody")]
        public bool AppendToBody { get; set; } = false;

        [HtmlAttributeName("DestroyOnClose")]
        public bool DestroyOnClose { get; set; } = false;

        [HtmlAttributeName("Draggable")]
        public bool Draggable { get; set; } = false;

        [HtmlAttributeName("Fullscreen")]
        public bool Fullscreen { get; set; } = false;

        [HtmlAttributeName("Center")]
        public bool Center { get; set; } = false;

        [HtmlAttributeName("AlignCenter")]
        public bool AlignCenter { get; set; } = false;

        [HtmlAttributeName("ShowClose")]
        public bool ShowClose { get; set; } = true;

        [HtmlAttributeName("IsModal")]
        public bool IsModal { get; set; } = true;

        [HtmlAttributeName("CloseOnClickModal")]
        public bool CloseOnClickModal { get; set; } = true;

        [HtmlAttributeName("CloseOnPressEscape")]
        public bool CloseOnPressEscape { get; set; } = true;

        [HtmlAttributeName("LockScroll")]
        public bool LockScroll { get; set; } = true;

        /// <summary>客户端关闭回调函数名（支持 window 上的路径，如 managerClientHandler）</summary>
        [HtmlAttributeName("CloseHandler")]
        public string CloseHandler { get; set; }

        /// <summary>服务端关闭回调处理器名（会 POST 到 ?handler={name}）</summary>
        [HtmlAttributeName("ServerCloseHandler")]
        public string ServerCloseHandler { get; set; }

        public override void Init(TagHelperContext context)
        {
            base.Init(context);
            context.Items[typeof(DialogContext)] = new DialogContext();
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "el-dialog";
            AddCommonAttributes(context, output);

            if (!string.IsNullOrWhiteSpace(Title))
                output.Attributes.SetAttribute("title", Title);
            if (!string.IsNullOrWhiteSpace(DialogWidth))
                output.Attributes.SetAttribute("width", DialogWidth);
            if (!string.IsNullOrWhiteSpace(Top))
                output.Attributes.SetAttribute("top", Top);

            if (AppendToBody)
                output.Attributes.SetAttribute("append-to-body", "true");
            if (DestroyOnClose)
                output.Attributes.SetAttribute("destroy-on-close", "true");
            if (Draggable)
                output.Attributes.SetAttribute("draggable", "true");
            if (Fullscreen)
                output.Attributes.SetAttribute("fullscreen", "true");
            if (Center)
                output.Attributes.SetAttribute("center", "true");
            if (AlignCenter)
                output.Attributes.SetAttribute("align-center", "true");

            if (!ShowClose)
                output.Attributes.SetAttribute(":show-close", "false");
            if (!IsModal)
                output.Attributes.SetAttribute(":modal", "false");
            if (!CloseOnClickModal)
                output.Attributes.SetAttribute(":close-on-click-modal", "false");
            if (!CloseOnPressEscape)
                output.Attributes.SetAttribute(":close-on-press-escape", "false");
            if (!LockScroll)
                output.Attributes.SetAttribute(":lock-scroll", "false");

            if (!string.IsNullOrWhiteSpace(CloseHandler) || !string.IsNullOrWhiteSpace(ServerCloseHandler))
            {
                output.Attributes.SetAttribute("v-on:closed", BuildClosedHandlerExpr());
            }

            var childContent = await output.GetChildContentAsync();
            var dialogContext = (DialogContext)context.Items[typeof(DialogContext)];

            var headerHtml = dialogContext.HeaderHtml.Length > 0
                ? $"<template #header>{dialogContext.HeaderHtml}</template>"
                : string.Empty;

            var bodyHtml = dialogContext.ContentHtml.Length > 0
                ? dialogContext.ContentHtml.ToString()
                : childContent.GetContent();

            var footerHtml = string.Empty;
            if (dialogContext.FooterHtml.Length > 0)
            {
                var justifyClass = dialogContext.FooterAlign switch
                {
                    DialogFooterAlign.Start => "justify-start",
                    DialogFooterAlign.Center => "justify-center",
                    DialogFooterAlign.SpaceBetween => "justify-between",
                    _ => "justify-end"
                };

                var footerStyle = string.IsNullOrWhiteSpace(dialogContext.FooterHeight)
                    ? string.Empty
                    : $" style=\"height:{dialogContext.FooterHeight};\"";

                footerHtml = $@"
                <template #footer>
                    <div class=""w-full flex items-center {justifyClass} gap-2""{footerStyle}>
                        {dialogContext.FooterHtml}
                    </div>
                </template>";
            }

            output.Content.SetHtmlContent($@"
                {headerHtml}
                {bodyHtml}
                {footerHtml}");
        }

        private string BuildClosedHandlerExpr()
        {
            var clientHandler = EscapeJs(CloseHandler);
            var serverHandler = EscapeJs(ServerCloseHandler);

            return $@"() => {{
                const __resolve = (path) => {{
                    if (!path || typeof path !== 'string') return null;
                    return path.split('.').reduce((obj, key) => obj && obj[key], window);
                }};

                const __client = '{clientHandler}';
                const __server = '{serverHandler}';

                if (__client) {{
                    const __fn = __resolve(__client);
                    if (typeof __fn === 'function') {{
                        try {{ __fn('close'); }} catch (err) {{ console.error(err); }}
                    }}
                }}

                if (__server) {{
                    EleManager.request('?handler=' + encodeURIComponent(__server), {{ action: 'close' }}, 'POST')
                        .then((res) => {{
                            if (res && (res.code === 0 || res.code === '0') && res.data && typeof res.data === 'object' && res.data.command) {{
                                EleManager.executeServerCommand(res.data);
                            }}
                        }})
                        .catch((err) => console.error(err));
                }}
            }}";
        }

        private static string EscapeJs(string value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
        }

    }

    /// <summary>
    /// 对话框头部插槽。
    /// </summary>
    [HtmlTargetElement("Header", ParentTag = "EleDialog")]
    [HtmlTargetElement("EleDialogHeader", ParentTag = "EleDialog")]
    public class EleDialogHeaderTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var dialogContext = (DialogContext)context.Items[typeof(DialogContext)];
            dialogContext.HeaderHtml.Append(childContent.GetContent());
            output.SuppressOutput();
        }
    }

    /// <summary>
    /// 对话框内容区。
    /// </summary>
    [HtmlTargetElement("Content", ParentTag = "EleDialog")]
    public class EleDialogContentTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var dialogContext = (DialogContext)context.Items[typeof(DialogContext)];
            dialogContext.ContentHtml.Append(childContent.GetContent());
            output.SuppressOutput();
        }
    }

    /// <summary>
    /// 对话框底部工具栏内容。
    /// </summary>
    [HtmlTargetElement("Footer", ParentTag = "EleDialog")]
    [HtmlTargetElement("EleDialogFooter", ParentTag = "EleDialog")]
    public class EleDialogFooterTagHelper : TagHelper
    {
        [HtmlAttributeName("Align")]
        public DialogFooterAlign Align { get; set; } = DialogFooterAlign.End;

        [HtmlAttributeName("Height")]
        public string Height { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var dialogContext = (DialogContext)context.Items[typeof(DialogContext)];
            dialogContext.FooterAlign = Align;
            dialogContext.FooterHeight = Height;
            dialogContext.FooterHtml.Append(childContent.GetContent());
            output.SuppressOutput();
        }
    }
}
