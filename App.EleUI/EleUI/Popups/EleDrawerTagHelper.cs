using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;
using System.Text;

namespace App.EleUI
{
    /// <summary>
    /// 抽屉方向。
    /// </summary>
    public enum DrawerDirection
    {
        Rtl,
        Ltr,
        Ttb,
        Btt
    }

    /// <summary>
    /// 抽屉上下文，包含一些子组件（如 EleDrawerFooter）需要共享的数据。
    /// </summary>
    public class DrawerContext
    {
        public StringBuilder ContentHtml { get; set; } = new StringBuilder();
        public StringBuilder FooterHtml { get; set; } = new StringBuilder();
        public DrawerFooterAlign FooterAlign { get; set; } = DrawerFooterAlign.Center;
        public string FooterHeight { get; set; }
    }

    /// <summary>
    /// 抽屉底部工具栏对齐方式。
    /// </summary>
    public enum DrawerFooterAlign
    {
        Start,
        Center,
        End,
        SpaceBetween
    }

    /// <summary>
    /// 抽屉组件标签帮助器。
    /// </summary>
    [HtmlTargetElement("EleDrawer")]
    public class EleDrawerTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("Title")]
        public string Title { get; set; }

        /// <summary>抽屉方向</summary>
        [HtmlAttributeName("Direction")]
        public DrawerDirection Direction { get; set; } = DrawerDirection.Rtl;

        [HtmlAttributeName("Size")]
        public string Size { get; set; } = "30%";
        
        [HtmlAttributeName("AppendToBody")]
        public bool AppendToBody { get; set; } = false;

        [HtmlAttributeName("WithHeader")]
        public bool WithHeader { get; set; } = true;

        /// <summary>是否显示遮罩层</summary>
        [HtmlAttributeName("IsModal")]
        public bool IsModal { get; set; } = true;

        /// <summary>当不为空时，内容区域显示 iframe 并填满</summary>
        [HtmlAttributeName("IFrameUrl")]
        public string IFrameUrl { get; set; }

        /// <summary>客户端关闭回调函数名（支持 window 上的路径，如 managerClientHandler）</summary>
        [HtmlAttributeName("CloseHandler")]
        public string CloseHandler { get; set; }

        /// <summary>服务端关闭回调处理器名（会 POST 到 ?handler={name}）</summary>
        [HtmlAttributeName("ServerCloseHandler")]
        public string ServerCloseHandler { get; set; }
        

        public override void Init(TagHelperContext context)
        {
            base.Init(context);
            context.Items[typeof(DrawerContext)] = new DrawerContext();
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "el-drawer";
            AddCommonAttributes(context, output);
            
            output.Attributes.SetAttribute("direction", Direction.ToString().ToLowerInvariant());
            output.Attributes.SetAttribute("size", Size);
            output.Attributes.SetAttribute(":show-close", "false");
            if (!string.IsNullOrWhiteSpace(CloseHandler) || !string.IsNullOrWhiteSpace(ServerCloseHandler))
            {
                output.Attributes.SetAttribute("v-on:closed", BuildClosedHandlerExpr());
            }
            
            if (AppendToBody) output.Attributes.SetAttribute("append-to-body", "true");
            if (!IsModal) output.Attributes.SetAttribute(":modal", "false");
            
            if (!WithHeader)
            {
                output.Attributes.SetAttribute(":with-header", "false");
            }

            var childContent = await output.GetChildContentAsync();
            var drawerContext = (DrawerContext)context.Items[typeof(DrawerContext)];
            
            string headerHtml = "";
            if (WithHeader)
            {
                var title = string.IsNullOrWhiteSpace(Title) ? "Drawer" : Title;
                var closeExpr = string.IsNullOrWhiteSpace(VModel) ? "" : $"v-on:click=\"{VModel} = false\"";
                headerHtml = $@"
                <template #header>
                    <div class=""w-full flex items-center justify-between"">
                        <span class=""font-bold text-lg"">{title}</span>
                        <button type=""button"" class=""text-gray-500 hover:text-gray-700"" {closeExpr}>
                            <el-icon><Close /></el-icon>
                        </button>
                    </div>
                </template>";
            }

            var bodyHtml = drawerContext.ContentHtml.Length > 0
                ? drawerContext.ContentHtml.ToString()
                : childContent.GetContent();
            if (!string.IsNullOrWhiteSpace(IFrameUrl))
            {
                bodyHtml = $@"<iframe src='{IFrameUrl}' style='width:100%;height:100%;border:0;'></iframe>";
            }

            var footerHtml = "";
            if (drawerContext.FooterHtml.Length > 0)
            {
                var justifyClass = drawerContext.FooterAlign switch
                {
                    DrawerFooterAlign.Start => "justify-start",
                    DrawerFooterAlign.End => "justify-end",
                    DrawerFooterAlign.SpaceBetween => "justify-between",
                    _ => "justify-center"
                };
                var footerStyle = string.IsNullOrWhiteSpace(drawerContext.FooterHeight)
                    ? ""
                    : $" style=\"height:{drawerContext.FooterHeight};\"";

                footerHtml = $@"
                <template #footer>
                    <div class=""w-full flex items-center {justifyClass} gap-2""{footerStyle}>
                        {drawerContext.FooterHtml}
                    </div>
                </template>";
            }

            output.Content.SetHtmlContent($@"
                {headerHtml}
                <div class=""h-full flex flex-col"">
                    <div class=""flex-1 min-h-0 overflow-auto"">
                        {bodyHtml}
                    </div>
                </div>
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
    /// 抽屉内容区。
    /// </summary>
    [HtmlTargetElement("Content", ParentTag = "EleDrawer")]
    public class EleDrawerContentTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var drawerContext = (DrawerContext)context.Items[typeof(DrawerContext)];
            drawerContext.ContentHtml.Append(childContent.GetContent());
            output.SuppressOutput();
        }
    }

    /// <summary>
    /// 抽屉底部工具栏内容。
    /// </summary>
    [HtmlTargetElement("Footer", ParentTag = "EleDrawer")]
    [HtmlTargetElement("EleDrawerFooter", ParentTag = "EleDrawer")]
    public class EleDrawerFooterTagHelper : TagHelper
    {
        [HtmlAttributeName("Align")]
        public DrawerFooterAlign Align { get; set; } = DrawerFooterAlign.Center;

        [HtmlAttributeName("Height")]
        public string Height { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var drawerContext = (DrawerContext)context.Items[typeof(DrawerContext)];
            drawerContext.FooterAlign = Align;
            drawerContext.FooterHeight = Height;
            drawerContext.FooterHtml.Append(childContent.GetContent());
            output.SuppressOutput();
        }
    }
}
