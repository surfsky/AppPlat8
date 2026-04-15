using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Threading.Tasks;
using App.DAL;
using App.Components;
using App.Utils;

namespace App.EleUI
{
    /// <summary>按钮类型（其实是色彩）</summary>
    public enum EleButtonType
    {
        Default,
        Primary,
        Success,
        Info,
        Warning,
        Danger,
    }


    /// <summary>
    /// 按钮外观（形状）。支持 Text、Plain、Round、Dashed、Circle 等多种外观。输出 <el-button> 元素。
    /// 参考：https://element-plus.org/en-US/component/button
    /// </summary>
    public enum EleButtonLook
    {
        /// <summary>Default look (fill background based on Type)</summary>
        Fill,
        /// <summary>Pure text</summary>
        Text,
        /// <summary>Border and text</summary>
        Plain,
        /// <summary>Round</summary>
        Round,
        /// <summary>Dashed</summary>
        Dashed,
        /// <summary>Circle</summary>
        Circle,
    }

    /// <summary>
    /// 按钮。支持不同类型、图标、命令等功能。输出 <el-button> 元素。
    /// 参考：https://element-plus.org/en-US/component/button
    /// 输出：
    /// <el-button plain>Plain</el-button>
    /// <el-button type="primary" plain>Primary</el-button>
    /// </summary>
    [HtmlTargetElement("EleButton")]
    public class EleButtonTagHelper : EleControlTagHelper
    {
        /// <summary>按钮类型</summary>
        [HtmlAttributeName("Type")]
        public EleButtonType Type { get; set; } = EleButtonType.Default;

        [HtmlAttributeName("Look")]
        public EleButtonLook Look { get; set; } = EleButtonLook.Fill;

        /// <summary>图标名称</summary>
        [HtmlAttributeName("Icon")]
        public EleIconName Icon { get; set; } = EleIconName.None;

        /// <summary>图标 CSS 类，如："fas fa-edit"</summary>
        [HtmlAttributeName("IconCls")]
        public string IconCls { get; set;}

        /// <summary>点击事件（原生 onclick）。作用域是当前 DOM 元素，不能直接访问 Vue 实例的 data、methods、props 等</summary>
        [HtmlAttributeName("Click")]
        public string Click { get; set; } 

        /// <summary>点击事件（Vue 表达式，输出 v-on:click）。作用域是当前 Vue 实例（组件实例），可以直接访问实例的 data、methods、props 等</summary>
        [HtmlAttributeName("VClick")]
        public string VClick { get; set; }

        /// <summary>
        /// 弹窗URL。用于简化在表格页面中的“新增/打开弹窗”按钮写法。
        /// 会生成：v-on:click="openForm(0, '...')"。
        /// </summary>
        [HtmlAttributeName("PopupUrl")]
        public string PopupUrl { get; set; }

        /// <summary>弹出页面标题。与 PopupUrl 配合使用。</summary>
        [HtmlAttributeName("PopupTitle")]
        public string PopupTitle { get; set; }

        /// <summary>命令类型。如果设置了命令类型，点击事件将自动绑定为 invokeCommand('{Command.ToString()}')</summary>
        [HtmlAttributeName("Command")]
        public Command Command { get; set; } // Default is None (0)

        /// <summary>命令名称。优先级高于 Command 属性，直接使用该字符串作为 v-on:click 的值</summary>
        [HtmlAttributeName("Handler")]
        public string Handler { get; set; }

        /// <summary>强类型文本绑定，输出 {{ path }}。</summary>
        [HtmlAttributeName("TextFor")]
        public ModelExpression TextFor { get; set; }

        /// <summary>加载中状态</summary>
        [HtmlAttributeName("Loading")]
        public string Loading { get; set; } // v-bind:loading

        /// <summary>处理标签</summary>
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) 
                return;

            if (IsSelectMode() && ShouldHideInSelectMode())
            {
                output.SuppressOutput();
                return;
            }

            output.TagName = "el-button";
            AddCommonAttributes(context, output);
            
            // type, outlook
            output.Attributes.SetAttribute("type", Type.ToString().ToLower());
            if (Look != EleButtonLook.Fill)  // Fill 是默认外观，不需要生成额外属性
                output.Attributes.Add(new TagHelperAttribute(Look.ToString().ToLower(), null, HtmlAttributeValueStyle.Minimized));  // 生成<el-button plain>，而不是<el-button plain="">
            
            // click priority:
            // 1) Handler -> v-on:click (direct post with form data)
            // 2) Command -> v-on:click (inner command pipeline)
            // 3) PopupUrl -> v-on:click openForm(0, url)
            // 4) Click -> native onclick
            // 5) VClick -> v-on:click
            if (!string.IsNullOrEmpty(Handler))     output.Attributes.SetAttribute("v-on:click", $"postHandler('{Handler}')");
            else if (Command != Command.None)
            {
                // 统一把 Search 路由到 Data，确保查询走 OnGetData。
                var commandName = Command == Command.Search ? "Data" : Command.ToString();
                output.Attributes.SetAttribute("v-on:click", $"invokeCommand('{commandName}')");
            }
            else if (!string.IsNullOrEmpty(PopupUrl))
            {
                var popupUrlExpr = PopupUrl.Replace("'", "\\'");
                if (!string.IsNullOrEmpty(PopupTitle))
                {
                    var popupTitleExpr = PopupTitle.Replace("'", "\\'");
                    output.Attributes.SetAttribute("v-on:click", $"openForm(0, '{popupUrlExpr}', '{popupTitleExpr}')");
                }
                else
                {
                    output.Attributes.SetAttribute("v-on:click", $"openForm(0, '{popupUrlExpr}')");
                }
            }
            else if (!string.IsNullOrEmpty(Click))  output.Attributes.SetAttribute("onclick", Click);
            else if (!string.IsNullOrEmpty(VClick)) output.Attributes.SetAttribute("v-on:click", VClick);

            // loading
            if (!string.IsNullOrEmpty(Loading)) 
                output.Attributes.SetAttribute(":loading", Loading);

            // text
            var childContent = await output.GetChildContentAsync();
            var buttonText = childContent.GetContent();
            var textExpr = GetBindPath(TextFor);
            if (!string.IsNullOrWhiteSpace(textExpr))
                buttonText = $"{{{{ {textExpr} }}}}";

            // icon
            var iconHtml = "";
            if (!string.IsNullOrEmpty(this.IconCls))
                iconHtml = $"<i class=\"{this.IconCls}\"></i> ";
            else if (Icon != EleIconName.None)
                iconHtml = $"<el-icon><component :is=\"'{this.Icon}'\"></component></el-icon> ";  // 用动态组件规避 link/view/filter/switch 等与原生标签同名导致的渲染冲突

            // icon + text
            if (!string.IsNullOrEmpty(iconHtml) && !string.IsNullOrEmpty(buttonText))
                output.Content.SetHtmlContent(iconHtml + $"<span class=\"ml-1\">{buttonText}</span>");
             else if (!string.IsNullOrEmpty(iconHtml))
                output.Content.SetHtmlContent(iconHtml);
             else if (!string.IsNullOrEmpty(buttonText))
                output.Content.SetHtmlContent(buttonText);
             else
                output.Content.SetHtmlContent(""); // 保持 <el-button></el-button>，不输出空格或换行
        }

        private bool ShouldHideInSelectMode()
        {
            if (Command == Command.Add || Command == Command.Edit || Command == Command.Delete || Command == Command.BatchDelete)
                return true;

            var handler = (Handler ?? string.Empty).Trim();
            if (string.Equals(handler, "Add", StringComparison.OrdinalIgnoreCase)
                || string.Equals(handler, "Edit", StringComparison.OrdinalIgnoreCase)
                || string.Equals(handler, "Delete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(handler, "BatchDelete", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
    }
}
