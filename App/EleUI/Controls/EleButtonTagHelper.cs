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

        /// <summary>图标名称，可以是 ElementPlus 组件名也可以是 css 类。如："el-icon-edit" 或 "fas fa-edit"</summary>
        [HtmlAttributeName("Icon")]
        public string Icon { get; set; }

        /// <summary>点击事件（原生 onclick）。作用域是当前 DOM 元素，不能直接访问 Vue 实例的 data、methods、props 等</summary>
        [HtmlAttributeName("Click")]
        public string Click { get; set; } 

        /// <summary>点击事件（Vue 表达式，输出 v-on:click）。作用域是当前 Vue 实例（组件实例），可以直接访问实例的 data、methods、props 等</summary>
        [HtmlAttributeName("VClick")]
        public string VClick { get; set; }

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

            output.TagName = "el-button";
            AddCommonAttributes(context, output);
            
            // type, outlook
            output.Attributes.SetAttribute("type", Type.ToString().ToLower());
            if (Look != EleButtonLook.Fill)  // Fill 是默认外观，不需要生成额外属性
                output.Attributes.Add(new TagHelperAttribute(Look.ToString().ToLower(), null, HtmlAttributeValueStyle.Minimized));  // 生成<el-button plain>，而不是<el-button plain="">
            
            // click priority:
            // 1) Handler -> v-on:click (direct post with form data)
            // 2) Command -> v-on:click (inner command pipeline)
            // 3) Click -> native onclick
            // 4) VClick -> v-on:click
            string clickAction = "";
            if (!string.IsNullOrEmpty(Handler))
            {
                clickAction = $"postHandler('{Handler}')";
                output.Attributes.SetAttribute("v-on:click", clickAction);
            }
            else if (Command != Command.None)
            {
                // - close/cancel: direct close() method
                // - add: open form with id=0
                // - all others: invokeCommand() to POST to ?handler={Command} on server
                if (Command == Command.Close || Command == Command.Cancel)
                    clickAction = "close";
                else if (Command == Command.Add)
                    clickAction = "openForm(0)";
                else
                    clickAction = $"invokeCommand('{Command.ToString()}')";
                output.Attributes.SetAttribute("v-on:click", clickAction);
            }
            else if (!string.IsNullOrEmpty(Click))
            {
                clickAction = Click;
                output.Attributes.SetAttribute("onclick", clickAction);
            }
            else if (!string.IsNullOrEmpty(VClick))
            {
                clickAction = VClick;
                output.Attributes.SetAttribute("v-on:click", clickAction);
            }

            // loading
            if (!string.IsNullOrEmpty(Loading)) 
                output.Attributes.SetAttribute(":loading", Loading);

            // icon
            var childContent = await output.GetChildContentAsync();
            var buttonText = childContent.GetContent();
            var textExpr = GetClientBindPath(TextFor);
            if (!string.IsNullOrWhiteSpace(textExpr))
                buttonText = $"{{{{ {textExpr} }}}}";

            if (!string.IsNullOrEmpty(Icon))
            {
                // 如果包含'fa-'，则认为是 fontawesome css 类
                // 否则把图标名字当成已注册的Element Plus 图标组件名，直接使用该标签
                string iconName = Icon;
                string iconHtml;
                if (iconName.Contains("fa-"))
                    iconHtml = $"<i class=\"{iconName}\"></i> ";
                else
                    iconHtml = $"<el-icon><{iconName} /></el-icon> ";
                output.Content.SetHtmlContent(iconHtml + buttonText);
            }
            else
            {
                output.Content.SetHtmlContent(buttonText);
            }
        }
    }
}
