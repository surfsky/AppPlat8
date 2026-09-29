using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;

namespace App.EleUI
{
    /// <summary>
    /// 单张图片展示控件：缩略（加?w=xxx）+ 点击在顶级窗口打开原图（避免 iframe 层级不够）
    /// 
    /// 用法 1 —— 静态 URL：
    ///   <EleImage Src="/upload/test.jpg" Width="64" Height="64" />
    /// 
    /// 用法 2 —— 表单模型绑定（在 EleForm 内，For="Item.Avatar" 将解析成 form.avatar）：
    ///   <EleImage For="Item.Avatar" Label="头像" ThumbWidth="128" />
    /// 
    /// 用法 3 —— EleList ItemTemplate / 自定义 Vue 表达式（如 item.image）：
    ///   <EleImage VueSrc="item.image" Width="64" Height="64" ThumbWidth="128" Rounded="true" />
    /// 
    /// 若传 PreviewList="urlsExpr" 表示点击时用该多张列表连续翻页（顶级窗口打开）。
    /// </summary>
    [HtmlTargetElement("EleImage")]
    public class EleImage : EleFormControl
    {
        [HtmlAttributeName("Src")]                  public string Src { get; set; }
        [HtmlAttributeName("VueSrc")]               public string VueSrc { get; set; }
        [HtmlAttributeName("ThumbWidth")]           public int ThumbWidth { get; set; } = 128;
        [HtmlAttributeName("Fit")]                  public string Fit { get; set; } = "cover";
        [HtmlAttributeName("Rounded")]              public bool Rounded { get; set; } = true;
        [HtmlAttributeName("Round")]                public bool Round { get; set; } = false;
        [HtmlAttributeName("ViewerMode")]           public string ViewerMode { get; set; } = "Top";
        [HtmlAttributeName("PreviewList")]          public string PreviewList { get; set; }
        [HtmlAttributeName("Alt")]                  public string Alt { get; set; }

        public EleImage() { }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            TryAutoSetLabel();

            bool inEleForm = context.Items.ContainsKey("IsEleForm");
            string vModel = null;
            if (For != null)
            {
                vModel = GetVModel(context); // EleForm 下是 form.xxx；Filter 下是 filters.xxx
            }

            // (1) 原图 Vue 表达式
            string vueExprOrStatic = ResolveOriginalUrlExpression(vModel);

            if (string.IsNullOrWhiteSpace(vueExprOrStatic))
            {
                // 未配置任何来源 → 抑制输出
                output.SuppressOutput();
                return;
            }

            // (2) 缩略图 Vue 表达式（用前端 Utils.withThumbWidth 统一处理 data:/blob:、已有 query 等情况）
            //  注意：EleFormAppBuilder 的 setup return 已经把 Utils (window.Utils) 注入到模板；如果没注入，也能安全引用 window.Utils
            string thumbExpr;
            if (ThumbWidth > 0)
            {
                if (vueExprOrStatic.StartsWith("'") && vueExprOrStatic.EndsWith("'"))
                {
                    var raw = vueExprOrStatic.Substring(1, vueExprOrStatic.Length - 2);
                    var withThumb = ServerSideAppendWidth(raw, ThumbWidth);
                    thumbExpr = $"'{withThumb.Replace("'", "\\'")}'";
                }
                else
                {
                    thumbExpr = $"Utils && typeof Utils.withThumbWidth === 'function' ? Utils.withThumbWidth(({vueExprOrStatic}), {ThumbWidth}) : (window && window.Utils && typeof window.Utils.withThumbWidth === 'function' ? window.Utils.withThumbWidth(({vueExprOrStatic}), {ThumbWidth}) : ({vueExprOrStatic}))";
                }
            }
            else
            {
                thumbExpr = $"({vueExprOrStatic})";
            }

            // (3) 预览（点击）逻辑
            string viewerFn;
            if (string.Equals(ViewerMode, "None", StringComparison.OrdinalIgnoreCase))
            {
                viewerFn = null;
            }
            else if (string.Equals(ViewerMode, "Current", StringComparison.OrdinalIgnoreCase))
            {
                viewerFn = "openImageViewer";
            }
            else
            {
                // 默认 Top：顶级窗口
                viewerFn = "Utils.openImageViewerTop";
                // 如果存在 EleForm 上下文中的方法（this.openImageViewerTop 已经由 EleForm prototype 继承 uploadMethods），
                // 优先用组件上下文的 openImageViewerTop（通常比 Utils.XXX 短，且兼容）
                if (inEleForm) viewerFn = "openImageViewerTop";
            }

            string clickHandler = null;
            if (!string.IsNullOrEmpty(viewerFn))
            {
                // 统一使用模板内可访问的 openTopImageViewer（由 EleFormAppBuilder/EleListAppBuilder 注入；没注入时退化到 window.Utils.openImageViewerTop）
                string clickFnName = inEleForm ? "openTopImageViewer" : "openTopImageViewer";

                if (!string.IsNullOrWhiteSpace(PreviewList))
                {
                    clickHandler = $@"{clickFnName}(({vueExprOrStatic}), ({PreviewList}), 0)";
                }
                else
                {
                    clickHandler = $@"{clickFnName}(({vueExprOrStatic}))";
                }

                // 兜底：如果页面模板没注入 openTopImageViewer（例如普通 Razor 页面没通过 EleForm/EleList mount），退到 window.Utils.openImageViewerTop
                // 做法：onClick 写成「(openTopImageViewer || ((window&&window.Utils)?window.Utils.openImageViewerTop:undefined))(args...)」
                string fallback = $"(typeof {clickFnName} !== 'undefined' ? {clickFnName} : ((typeof window !== 'undefined' && window.Utils && typeof window.Utils.openImageViewerTop === 'function') ? window.Utils.openImageViewerTop : null))";
                if (!string.IsNullOrWhiteSpace(PreviewList))
                {
                    clickHandler = $"{fallback}(({vueExprOrStatic}), ({PreviewList}), 0)";
                }
                else
                {
                    clickHandler = $"{fallback}(({vueExprOrStatic}))";
                }
            }

            // (4) 样式
            StringBuilder styleSb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(Width))
            {
                var w = NormalizePixel(Width);
                if (!string.IsNullOrEmpty(w)) styleSb.Append("width:").Append(w).Append(';');
            }
            else
            {
                styleSb.Append("width:64px;");
            }
            if (!string.IsNullOrWhiteSpace(Height))
            {
                var h = NormalizePixel(Height);
                if (!string.IsNullOrEmpty(h)) styleSb.Append("height:").Append(h).Append(';');
            }
            else
            {
                styleSb.Append("height:64px;");
            }
            if (Round)
            {
                styleSb.Append("border-radius:9999px;");
            }
            else if (Rounded)
            {
                styleSb.Append("border-radius:4px;");
            }
            if (!string.IsNullOrWhiteSpace(Border))
            {
                // 用基类处理的 Border 可能已经在 style 里，这里额外再追加一次 Border 属性
                if (Regex.IsMatch(Border.Trim(), @"^\d+$"))
                    styleSb.Append("border:").Append(Border.Trim()).Append("px solid var(--el-border-color-light);");
                else
                    styleSb.Append("border:").Append(Border.Trim()).Append(';');
            }
            else
            {
                styleSb.Append("border:1px solid var(--el-border-color-light);");
            }
            if (!string.IsNullOrWhiteSpace(clickHandler))
                styleSb.Append("cursor:pointer;");

            output.TagName = "el-image";
            output.TagMode = TagMode.StartTagAndEndTag;

            output.Attributes.SetAttribute(":src", thumbExpr);
            output.Attributes.SetAttribute(":preview-src-list", "[]");
            if (!string.IsNullOrWhiteSpace(Fit))
                output.Attributes.SetAttribute("fit", Fit);
            if (!string.IsNullOrWhiteSpace(Alt))
                output.Attributes.SetAttribute("alt", Alt);
            if (styleSb.Length > 0)
                output.Attributes.SetAttribute("style", styleSb.ToString());
            if (!string.IsNullOrWhiteSpace(clickHandler))
                output.Attributes.SetAttribute("@click.stop.prevent", clickHandler);

            // 透传剩余的未处理属性（用户可能直接写 class、loading、lazy 等）
            // （TagHelper 默认会把未处理的 attribute 保留，所以不用额外处理。）

            // (5) 如果有 Label → 走 el-form-item 包裹（EleForm 上下文内）；否则不包
            if (!string.IsNullOrEmpty(Label) && inEleForm)
            {
                await RenderWrapper(output);
            }
        }

        /// <summary>
        /// 解析出"原图地址对应的 Vue 表达式"
        /// 优先级：VueSrc > For(vModel) > Src
        /// 其中静态 Src 输出为 JS 单引号字符串；其他情况输出对应的 Vue 表达式。
        /// </summary>
        private string ResolveOriginalUrlExpression(string vModel)
        {
            if (!string.IsNullOrWhiteSpace(VueSrc))
            {
                var expr = VueSrc.Trim();
                // 兼容用户以 @item.Image 开头（少写了 VueSrc 的冒号也允许）
                if (expr.StartsWith("@")) expr = expr.Substring(1);
                return NormalizeItemPath(expr);
            }

            if (!string.IsNullOrWhiteSpace(vModel))
                return vModel; // form.xxx 或 filters.xxx

            if (!string.IsNullOrWhiteSpace(Src))
            {
                var raw = Src.Trim();
                if (raw.Length == 0) return null;
                // 返回 JS 字符串字面量（单引号）
                return "'" + raw.Replace("'", "\\'") + "'";
            }

            return null;
        }

        /// <summary>
        /// 兼容用户把 "Item.Image" 写成 "item.Image"，并把形如 "Item.ImageUrls[i]" 的 C# 风格路径转成 Vue 作用域下的 item.imageUrls[i]
        /// </summary>
        private static string NormalizeItemPath(string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return expr;
            var e = expr.Trim();
            if (e.StartsWith("Item.", StringComparison.OrdinalIgnoreCase))
            {
                var rest = e.Substring("Item.".Length);
                if (rest.Length == 0) return "item";
                rest = char.ToLowerInvariant(rest[0]) + rest.Substring(1);
                return "item." + rest;
            }
            return e;
        }

        private static string ServerSideAppendWidth(string url, int width)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (Regex.IsMatch(url, @"^\s*data:|^\s*blob:", RegexOptions.IgnoreCase)) return url;
            try
            {
                int hash = url.IndexOf('#');
                string main = hash >= 0 ? url.Substring(0, hash) : url;
                string frag = hash >= 0 ? url.Substring(hash) : "";
                if (Regex.IsMatch(main, @"[?&](?:w|width|tw|thumbnailWidth)=\d+", RegexOptions.IgnoreCase)) return url;
                bool hasQ = main.IndexOf('?') >= 0;
                return main + (hasQ ? "&" : "?") + "w=" + width + frag;
            }
            catch
            {
                return url;
            }
        }

        private static string NormalizePixel(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            var s = val.Trim();
            if (Regex.IsMatch(s, @"^\d+$")) return s + "px";
            return s;
        }
    }
}
