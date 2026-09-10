using System;
using System.Collections.Generic;

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

    /// <summary>BatchField 元数据定义（纯帮助类，不作为 TagHelper 注册）。
    /// Razor 中写的 &lt;BatchField ... /&gt; 保持为纯字符串，由 EleButton 通过
    /// BatchFieldExtractor 正则解析，避免 TagHelper 管道因 ParentTag/Items 时序吞掉子标签。</summary>
    public class BatchFieldTagHelper
    {
        public EleBatchFieldControl Control { get; set; } = EleBatchFieldControl.Input;
        public string Field { get; set; }
        public string Label { get; set; }
        public string Placeholder { get; set; }
        public bool Required { get; set; } = false;
        public bool Enabled { get; set; } = true;

        public string PopupUrl { get; set; }
        public string TextFor { get; set; }
        public bool Multi { get; set; } = false;

        public string Options { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? Step { get; set; }
        public int Rows { get; set; } = 3;

        public Dictionary<string, object> ToMeta()
        {
            var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            d["control"]    = Control.ToString().ToLowerInvariant();
            d["field"]      = Field ?? "";
            d["label"]      = Label ?? Field ?? "";
            d["placeholder"]= Placeholder ?? "";
            d["required"]   = Required;
            d["enabled"]    = Enabled;
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
            return d;
        }
    }

    /// <summary>占位帮助类；真实的 <BatchFields> 包裹标签不作为 TagHelper 执行，保留为纯字符串以便正则提取。</summary>
    public class BatchFieldsTagHelper { }
}
