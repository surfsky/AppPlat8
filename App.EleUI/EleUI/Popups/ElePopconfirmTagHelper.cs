using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// 气泡确认框容器。输出 el-popconfirm。
    /// </summary>
    [HtmlTargetElement("ElePopconfirm")]
    public class ElePopconfirmTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("Title")]
        public string Title { get; set; }

        [HtmlAttributeName("Confirm")]
        public string Confirm { get; set; }

        [HtmlAttributeName("Cancel")]
        public string Cancel { get; set; }

        [HtmlAttributeName("ConfirmText")]
        public string ConfirmText { get; set; }

        [HtmlAttributeName("CancelText")]
        public string CancelText { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output))
                return;

            output.TagName = "el-popconfirm";
            AddCommonAttributes(context, output);

            if (!string.IsNullOrWhiteSpace(Title))
                output.Attributes.SetAttribute("title", Title);
            if (!string.IsNullOrWhiteSpace(Confirm))
                output.Attributes.SetAttribute("v-on:confirm", Confirm);
            if (!string.IsNullOrWhiteSpace(Cancel))
                output.Attributes.SetAttribute("v-on:cancel", Cancel);
            if (!string.IsNullOrWhiteSpace(ConfirmText))
                output.Attributes.SetAttribute("confirm-button-text", ConfirmText);
            if (!string.IsNullOrWhiteSpace(CancelText))
                output.Attributes.SetAttribute("cancel-button-text", CancelText);
        }
    }
}