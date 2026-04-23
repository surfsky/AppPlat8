using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace App.EleUI
{
    public enum EleDatePickerType
    {
        Date,
        DateTime,
        //Week,
        //Month,
        //Year,
        //Dates,
        DateRange,
        DateTimeRange
    }

    [HtmlTargetElement("EleDatePicker")]
    public class EleDatePickerTagHelper : EleFormControlTagHelper
    {
        [HtmlAttributeName("Type")]
        public EleDatePickerType Type { get; set; } = EleDatePickerType.Date;

        [HtmlAttributeName("Format")]
        public string Format { get; set; } = "YYYY-MM-DD";

        [HtmlAttributeName("ValueFormat")]
        public string ValueFormat { get; set; }

        [HtmlAttributeName("StartPlaceholder")]
        public string StartPlaceholder { get; set; }

        [HtmlAttributeName("EndPlaceholder")]
        public string EndPlaceholder { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;
            output.TagName = "el-date-picker";
            AddCommonAttributes(context, output);

            // Type
            output.Attributes.SetAttribute("type", Type.ToString().ToLower());
            
            // ValueFormat
            if (string.IsNullOrEmpty(ValueFormat))
            {
                if (Type == EleDatePickerType.DateTime || Type == EleDatePickerType.DateTimeRange)
                    output.Attributes.SetAttribute("value-format", "YYYY-MM-DDTHH:mm:ss"); // ISO
                else
                    output.Attributes.SetAttribute("value-format", "YYYY-MM-DD");
            }
            else
            {
                output.Attributes.SetAttribute("value-format", ValueFormat);
            }

            // Placeholder
            if (!string.IsNullOrEmpty(StartPlaceholder)) output.Attributes.SetAttribute("start-placeholder", StartPlaceholder);
            if (!string.IsNullOrEmpty(EndPlaceholder))   output.Attributes.SetAttribute("end-placeholder", EndPlaceholder);

            await RenderWrapper(output);
        }
    }
}
