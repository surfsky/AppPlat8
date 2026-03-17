using Microsoft.AspNetCore.Razor.TagHelpers;
using System;
using System.Threading.Tasks;

namespace App.EleUI
{
    /// <summary>
    /// 图标选择器组件
    /// </summary>
    [HtmlTargetElement("EleIconSelect")]
    public class EleIconSelectTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Popup")]
        public string Popup { get; set; }

        [HtmlAttributeName("Multi")]
        public bool Multi { get; set; } = false;

        [HtmlAttributeName("TextProp")]
        public string TextProp { get; set; }

        [HtmlAttributeName("ItemWidth")]
        public int ItemWidth { get; set; } = 100;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "div";
            
            var vModel = GetVModel(context);
            var propName = GetPropName();
            var textProp = TextProp;

            // 对于图标选择器，通常值和显示文本是同一个（图标类名）
            if (string.IsNullOrEmpty(textProp) && For != null)
            {
                var name = For.Name;
                if (name.Contains(".")) 
                    name = name.Substring(name.LastIndexOf('.') + 1);
                textProp = ToCamelCase(name);
            }
            else if (!string.IsNullOrEmpty(textProp))
            {
                // 确保 TextProp 被转换为小驼峰（JavaScript 对象键需要小驼峰）
                textProp = ToCamelCase(textProp);
            }

            var popupUrl = !string.IsNullOrEmpty(Popup) ? Popup : "/Shared/IconSelector";
            var title = Label ?? "选择图标";
            var multiStr = Multi.ToString().ToLower();
            var formModel = context.Items.ContainsKey("EleFormModel") ? context.Items["EleFormModel"] as string : "form";
            var boxWidth = ItemWidth > 0 ? ItemWidth : 100;
            var boxStyle = $"width:{boxWidth}px; height:{boxWidth}px;";
            var iconFontSize = Math.Max(24, Math.Min(48, boxWidth / 3));

            await RenderWrapper(output);
            
            output.Attributes.SetAttribute("style", "width: 100%");
            output.TagName = "div";
            output.Attributes.SetAttribute("class", "ele-selector-wrapper");
            
            // 自定义模板：显示图标预览
            // 当有值时，显示一个类似 ImageUpload 的预览框，但内容是图标
            var content = $@"
            <div class=""ele-icon-selector"">
                <div v-if=""{formModel}.{textProp}"" class=""relative inline-block group overflow-visible"">
                     <!-- 图标预览框 -->
                            <div style=""{boxStyle}"" class=""border border-gray-200 rounded flex flex-col justify-center items-center cursor-pointer hover:bg-gray-50 transition-colors""
                          @click=""openSelector('{propName}', '{textProp}', '{popupUrl}', {multiStr}, '{title}')"">
                                <i :class=""{formModel}.{textProp}"" style=""font-size: {iconFontSize}px; color: #333; margin-bottom: 8px;""></i>
                     </div>
                     <!-- 删除按钮：橙色填充 + 白色线风格 -->
                     <div class=""absolute -top-3 -right-3 z-20"" @click.stop=""clearSelector('{propName}', '{textProp}')"">
                        <div class=""ele-corner-close-btn"" title=""清除图标"">
                            <el-icon class=""ele-corner-close-elicon""><Close /></el-icon>
                        </div>
                     </div>
                </div>

                <!-- 添加按钮 -->
                 <div v-else style=""{boxStyle}"" class=""border border-dashed border-gray-300 rounded flex flex-col justify-center items-center cursor-pointer hover:border-blue-400 hover:text-blue-400 transition-colors""
                     @click=""openSelector('{propName}', '{textProp}', '{popupUrl}', {multiStr}, '{title}')"">
                    <el-icon class=""text-3xl text-gray-400""><Plus /></el-icon>
                    <span class=""text-xs text-gray-400 mt-2"">选择图标</span>
                </div>
            </div>
            ";
            
            output.Content.SetHtmlContent(content);
        }
    }
}
