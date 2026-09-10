using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    public enum EleBatchFieldControl
    {
        Input,
        Textarea,
        Number,
        Switch,
        DatePicker,
        DateTimePicker,
        Select,
        Picker
    }

    /// <summary>批量字段上下文：存放在 EleButton 的 context.Items 中，供 BatchField 子标签写入元数据。</summary>
    public class BatchFieldsContext
    {
        public List<Dictionary<string, object>> Fields { get; } = new List<Dictionary<string, object>>();
    }

    //-------------------------------------------------------------------------
    // BatchField 子标签：直接写 Control/Field/Label/Options 等强类型属性
    //-------------------------------------------------------------------------
    /// <summary>单个批量字段声明。必须作为 <BatchFields> 的直接子元素使用。</summary>
    [HtmlTargetElement("BatchField", ParentTag = "BatchFields")]
    public class BatchFieldTagHelper : TagHelper
    {
        [HtmlAttributeName("Control")]      public EleBatchFieldControl Control { get; set; } = EleBatchFieldControl.Input;
        [HtmlAttributeName("Field")]        public string Field { get; set; }
        [HtmlAttributeName("Label")]        public string Label { get; set; }
        [HtmlAttributeName("Placeholder")]  public string Placeholder { get; set; }
        [HtmlAttributeName("Required")]     public bool Required { get; set; } = false;
        [HtmlAttributeName("Enabled")]      public bool Enabled { get; set; } = true;

        [HtmlAttributeName("PopupUrl")]     public string PopupUrl { get; set; }
        [HtmlAttributeName("TextFor")]      public string TextFor { get; set; }
        [HtmlAttributeName("Multi")]        public bool Multi { get; set; } = false;

        [HtmlAttributeName("Options")]      public string Options { get; set; }
        [HtmlAttributeName("Min")]          public double? Min { get; set; }
        [HtmlAttributeName("Max")]          public double? Max { get; set; }
        [HtmlAttributeName("Step")]         public double? Step { get; set; }
        [HtmlAttributeName("Rows")]         public int Rows { get; set; } = 3;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            // 从父级 EleButton 放进来的 BatchFieldsContext 拿列表
            if (!(context.Items[typeof(BatchFieldsContext)] is BatchFieldsContext ctx))
            {
                output.SuppressOutput();
                return;
            }

            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["control"]     = Control.ToString().ToLowerInvariant(),
                ["field"]       = Field ?? "",
                ["label"]       = Label ?? Field ?? "",
                ["placeholder"] = Placeholder ?? "",
                ["required"]    = Required,
                ["enabled"]     = Enabled
            };
            if (!string.IsNullOrEmpty(PopupUrl)) d["popupUrl"] = PopupUrl;
            if (!string.IsNullOrEmpty(TextFor))  d["textFor"]  = TextFor;
            if (Multi)                           d["multi"]    = true;
            if (!string.IsNullOrEmpty(Options))
            {
                var arr = new List<Dictionary<string, object>>();
                foreach (var raw in (Options ?? "").Split(',', '，', '|', ';'))
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    var s = raw.Trim();
                    string label, value;
                    int eq = s.IndexOf('=');
                    if (eq > 0) { label = s.Substring(0, eq).Trim(); value = s.Substring(eq + 1).Trim(); }
                    else        { label = s; value = s; }
                    arr.Add(new Dictionary<string, object> { ["label"] = label, ["value"] = value });
                }
                d["options"] = arr;
            }
            if (Min.HasValue)  d["min"]  = Min.Value;
            if (Max.HasValue)  d["max"]  = Max.Value;
            if (Step.HasValue) d["step"] = Step.Value;
            if (Rows > 0)      d["rows"] = Rows;

            ctx.Fields.Add(d);
            output.SuppressOutput();
        }
    }

    //-------------------------------------------------------------------------
    // BatchFields 包裹容器：ParentTag = EleButton，负责触发子 BatchField 执行
    //-------------------------------------------------------------------------
    /// <summary>批量字段声明容器。直接内嵌在 EleButton 标签内使用。</summary>
    [HtmlTargetElement("BatchFields", ParentTag = "EleButton")]
    public class BatchFieldsTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            // 确保父 EleButton 的 Init 已经写入 BatchFieldsContext
            if (!(context.Items[typeof(BatchFieldsContext)] is BatchFieldsContext _))
            {
                output.SuppressOutput();
                return;
            }
            // 执行子 BatchField（他们会把自己写入 BatchFieldsContext.Fields）
            await output.GetChildContentAsync();
            output.SuppressOutput();
        }
    }
}
