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
    /// 基础项容器：封装通用 Tailwind 属性（尺寸、边框、圆角、阴影、内外边距、背景等）。
    /// </summary>
    [HtmlTargetElement("Item")]
    public class EleItemTagHelper : EleControlTagHelper
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

        /// <summary>组合类名，根据属性值和布局类名生成最终的类名</summary>
        protected string ComposeClass(TagHelperOutput output, string layoutClass)
        {
            var classes = new List<string>();
            if (!string.IsNullOrWhiteSpace(layoutClass))
                classes.Add(layoutClass.Trim());

            switch (Overflow)
            {
                case OverflowBehavior.Hidden: classes.Add("overflow-hidden");  break;
                case OverflowBehavior.Auto:   classes.Add("overflow-auto");    break;
                case OverflowBehavior.AutoX:  classes.Add("overflow-x-auto");  break;
                case OverflowBehavior.AutoY:  classes.Add("overflow-y-auto");  break;
            }
            AddCssClass(classes, W, "w-");
            AddCssClass(classes, H, "h-");
            AddCssClass(classes, MinW, "min-w-");
            AddCssClass(classes, MaxW, "max-w-");
            AddCssClass(classes, MinH, "min-h-");
            AddCssClass(classes, MaxH, "max-h-");

            AddBorderClass(classes, Border);
            AddCssClass(classes, BorderColor, "border-");
            AddCssClass(classes, Rounded, "rounded-");
            AddCssClass(classes, Shadow, "shadow-");

            AddCssClass(classes, P, "p-");
            AddCssClass(classes, Px, "px-");
            AddCssClass(classes, Py, "py-");
            AddCssClass(classes, M, "m-");
            AddCssClass(classes, Mx, "mx-");
            AddCssClass(classes, My, "my-");
            AddCssClass(classes, Bg, "bg-");

            var existingClass = output.Attributes["class"]?.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(existingClass))
                classes.Add(existingClass.Trim());

            return string.Join(" ", classes);
        }
    }
}