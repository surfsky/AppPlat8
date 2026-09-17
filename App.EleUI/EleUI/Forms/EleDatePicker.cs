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
    public class EleDatePicker : EleFormControl
    {
        [HtmlAttributeName("Type")]               public EleDatePickerType Type { get; set; } = EleDatePickerType.Date;
        [HtmlAttributeName("Format")]             public string Format { get; set; } = "YYYY-MM-DD";
        [HtmlAttributeName("ValueFormat")]        public string ValueFormat { get; set; }
        [HtmlAttributeName("StartPlaceholder")]   public string StartPlaceholder { get; set; }
        [HtmlAttributeName("EndPlaceholder")]     public string EndPlaceholder { get; set; }

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

            // Default value (e.g. from URL parameter via page handler).
            // Pre-populates the EleTable filter so the date picker shows the
            // right value and the list auto-loads with the filter applied.
            TrySetFilterDefault(context, output);

            // Also set initial model-value so the picker shows a date on first
            // paint (before the table builder runs data-filter-default scan).
            // NOTE: In filter context (EleTable toolbar) we MUST NOT emit a
            // static :model-value, because the same element already has
            // v-model="filters.xxx". Element Plus v3 treats :model-value as
            // one-way controlled prop when paired with value-format, causing
            // the calendar panel's @update:model-value to be swallowed and
            // the user to be unable to change the date. The initial value is
            // still propagated via data-filter-default →
            // EleAppBuilder.applyFilterDefaults on mount, which writes into
            // the reactive filters ref (backed by v-model).
            if (Value != null && context.Items.ContainsKey("IsEleForm"))
            {
                var defaultExpr = GetDefaultValueExpression();
                if (!string.IsNullOrWhiteSpace(defaultExpr) && !output.Attributes.ContainsName(":model-value"))
                {
                    output.Attributes.SetAttribute(":model-value", defaultExpr);
                }
            }

            await RenderWrapper(output);
        }
    }
}
