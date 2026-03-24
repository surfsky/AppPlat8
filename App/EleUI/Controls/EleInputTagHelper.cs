using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;
using System.Net;

namespace App.EleUI
{
    public enum EleInputType
    {
        Text,
        TextArea,
        Password,
        Search,
    }



    [HtmlTargetElement("EleInput")]
    public class EleInputTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Type")]
        public EleInputType Type { get; set; } = EleInputType.Text;

        [HtmlAttributeName("Value")]
        public string Value { get; set; } // Manual v-model override

        [HtmlAttributeName("Rows")]
        public int Rows { get; set; }

        [HtmlAttributeName("Prepend")]
        public string Prepend { get; set; }

        [HtmlAttributeName("Append")]
        public string Append { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            output.TagName = "el-input";
            AddCommonAttributes(context, output);

            // Set v-model data
            if (!string.IsNullOrEmpty(Value))           output.Attributes.SetAttribute("v-model", Value); // Override

            // Type
            if (Type != EleInputType.Text)
            {
                if (Type == EleInputType.TextArea)      output.Attributes.SetAttribute("type", "textarea");
                else if (Type == EleInputType.Password) output.Attributes.SetAttribute("type", "password");
            }
            if (Rows > 0) 
            {
                this.Type = EleInputType.TextArea;
                output.Attributes.SetAttribute("type", "textarea");
                output.Attributes.SetAttribute(":rows", Rows);
            }

            // Child
            var childContent = await output.GetChildContentAsync();
            var contentHtml = childContent.GetContent();
            if (!string.IsNullOrWhiteSpace(Prepend))   contentHtml = $"<template #prepend>{WebUtility.HtmlEncode(Prepend)}</template>" + contentHtml;
            if (!string.IsNullOrWhiteSpace(Append))    contentHtml += $"<template #append>{WebUtility.HtmlEncode(Append)}</template>";
            if (!string.IsNullOrEmpty(contentHtml))    output.Content.SetHtmlContent(contentHtml);

            //
            await RenderWrapper(output);
        }
    }
}
