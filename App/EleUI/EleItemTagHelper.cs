using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Collections.Generic;

namespace App.EleUI
{
    //   - overflow-hidden overflow-auto overflow-x-auto overflow-y-auto
    public enum OverflowBehavior
    {
        Hidden,
        Auto,
        AutoX,
        AutoY,
    }

    /// <summary>
    /// 布局基础容器：封装通用 Tailwind 属性（尺寸、边框、圆角、阴影、内外边距、背景等）。
    /// </summary>
    [HtmlTargetElement("Item")]
    public abstract class EleItemTagHelper : EleControlTagHelper
    {

        // 溢出行为
        [HtmlAttributeName("Overflow")]
        public OverflowBehavior Overflow { get; set; } = OverflowBehavior.AutoY;

        [HtmlAttributeName("W")]
        public string W { get; set; }

        [HtmlAttributeName("H")]
        public string H { get; set; }

        [HtmlAttributeName("MinW")]
        public string MinW { get; set; }

        [HtmlAttributeName("MaxW")]
        public string MaxW { get; set; }

        [HtmlAttributeName("MinH")]
        public string MinH { get; set; }

        [HtmlAttributeName("MaxH")]
        public string MaxH { get; set; }

        [HtmlAttributeName("Border")]
        public bool Border { get; set; }

        [HtmlAttributeName("BorderColor")]
        public string BorderColor { get; set; }

        [HtmlAttributeName("Rounded")]
        public string Rounded { get; set; }

        [HtmlAttributeName("Shadow")]
        public new string Shadow { get; set; }

        [HtmlAttributeName("P")]
        public string P { get; set; }

        [HtmlAttributeName("Px")]
        public string Px { get; set; }

        [HtmlAttributeName("Py")]
        public string Py { get; set; }

        [HtmlAttributeName("M")]
        public string M { get; set; }

        [HtmlAttributeName("Mx")]
        public string Mx { get; set; }

        [HtmlAttributeName("My")]
        public string My { get; set; }

        [HtmlAttributeName("Bg")]
        public string Bg { get; set; }

        protected string ComposeClass(TagHelperOutput output, string layoutClass)
        {
            var classes = new List<string>();
            if (!string.IsNullOrWhiteSpace(layoutClass))
                classes.Add(layoutClass.Trim());

            switch (Overflow)
            {
                case OverflowBehavior.Hidden: classes.Add("overflow-hidden"); break;
                case OverflowBehavior.Auto:   classes.Add("overflow-auto");     break;
                case OverflowBehavior.AutoX:  classes.Add("overflow-x-auto");  break;
                case OverflowBehavior.AutoY:  classes.Add("overflow-y-auto");  break;
            }
            AddUtilityClass(classes, W, "w-");
            AddUtilityClass(classes, H, "h-");
            AddUtilityClass(classes, MinW, "min-w-");
            AddUtilityClass(classes, MaxW, "max-w-");
            AddUtilityClass(classes, MinH, "min-h-");
            AddUtilityClass(classes, MaxH, "max-h-");

            if (Border)
                classes.Add("border");
            AddUtilityClass(classes, BorderColor, "border-");
            AddUtilityClass(classes, Rounded, "rounded-");
            AddUtilityClass(classes, Shadow, "shadow-");

            AddUtilityClass(classes, P, "p-");
            AddUtilityClass(classes, Px, "px-");
            AddUtilityClass(classes, Py, "py-");
            AddUtilityClass(classes, M, "m-");
            AddUtilityClass(classes, Mx, "mx-");
            AddUtilityClass(classes, My, "my-");
            AddUtilityClass(classes, Bg, "bg-");

            var existingClass = output.Attributes["class"]?.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(existingClass))
                classes.Add(existingClass.Trim());

            return string.Join(" ", classes);
        }

        private static void AddUtilityClass(List<string> classes, string value, string prefix)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var trimmed = value.Trim();
            if (trimmed.StartsWith(prefix))
                classes.Add(trimmed);
            else
                classes.Add(prefix + trimmed);
        }
    }
}