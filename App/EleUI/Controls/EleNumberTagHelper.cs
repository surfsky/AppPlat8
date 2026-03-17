using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;
using System.Net;

namespace App.EleUI
{
    /// <summary>
    /// 数值输入框。支持小数位、步长、前后缀、控制按钮位置等功能。
    /// 参考：https://element-plus.org/en-US/component/input-number
    /// 输出：<el-input-number v-model="num" :min="1" :max="10" @change="handleChange" />
    /// </summary>
    [HtmlTargetElement("EleNumber")]
    public class EleNumberTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Precision")]
        public int Precision { get; set; } = 0;

        [HtmlAttributeName("Step")]
        public double Step { get; set; } = 0;

        [HtmlAttributeName("Prefix")]
        public string Prefix { get; set; }

        [HtmlAttributeName("Suffix")]
        public string Suffix { get; set; }

        [HtmlAttributeName("Prepend")]
        public string Prepend { get; set; }

        [HtmlAttributeName("Append")]
        public string Append { get; set; }

        [HtmlAttributeName("ControlPosition")]
        public string ControlPosition { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "el-input-number";

            // attributes: precision, step, controls-position
            AddCommonAttributes(context, output);
            if (Precision > 0) output.Attributes.Add("precision", Precision);
            if (Step > 0)      output.Attributes.Add("step", Step);
            if (!string.IsNullOrWhiteSpace(ControlPosition)) output.Attributes.SetAttribute("controls-position", ControlPosition.ToLower());

            var childContent = await output.GetChildContentAsync();
            var contentHtml = childContent.GetContent();

            // prefix & suffix
            var effectivePrefix = string.IsNullOrWhiteSpace(Prefix) ? Prepend : Prefix;
            var effectiveSuffix = string.IsNullOrWhiteSpace(Suffix) ? Append : Suffix;
            if (!string.IsNullOrWhiteSpace(effectivePrefix))
            {
                contentHtml = $"<template #prefix><span>{WebUtility.HtmlEncode(effectivePrefix)}</span></template>" + contentHtml;
            }
            if (!string.IsNullOrWhiteSpace(effectiveSuffix))
            {
                contentHtml += $"<template #suffix><span>{WebUtility.HtmlEncode(effectiveSuffix)}</span></template>";
            }

            if (!string.IsNullOrEmpty(contentHtml))
                output.Content.SetHtmlContent(contentHtml);

            await RenderWrapper(output);
        }
    }
}
