using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>表格超链接列</summary>
    [HtmlTargetElement("EleLinkColumn", ParentTag = "Columns")]
    public class EleLinkColumn : EleColumn
    {
        [HtmlAttributeName("TextFor")] public ModelExpression TextFor { get; set; }
        [HtmlAttributeName("TextProp")] public string TextProp { get; set; }
        [HtmlAttributeName("Text")] public string Text { get; set; }
        [HtmlAttributeName("Display")] public EleLinkDisplay Display { get; set; } = EleLinkDisplay.Blank;
        [HtmlAttributeName("DrawerTitle")] public string DrawerTitle { get; set; }
        [HtmlAttributeName("DrawerSize")] public string DrawerSize { get; set; }

        /// <summary>渲染超链接列</summary>
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            await base.ProcessAsync(context, output);

            var hrefProp = ResolveClientProp(Prop, For);
            if (string.IsNullOrWhiteSpace(hrefProp))
                return;

            var textProp = ResolveClientProp(TextProp, TextFor);
            if (string.IsNullOrWhiteSpace(textProp))
                textProp = hrefProp;

            var textExpr = string.IsNullOrWhiteSpace(Text)
                ? $"(scope.row.{textProp} ?? scope.row.{hrefProp} ?? '')"
                : $"'{EscapeJs(Text)}'";

            if (Display == EleLinkDisplay.Drawer)
            {
                var title = string.IsNullOrWhiteSpace(DrawerTitle) ? (Label ?? "查看") : DrawerTitle;
                var sizeArg = string.IsNullOrWhiteSpace(DrawerSize) ? "null" : $"'{EscapeJs(DrawerSize)}'";
                output.Content.SetHtmlContent($@"
                        <template #default=""scope"">
                            <a href=""javascript:void(0)"" class=""text-blue-600 hover:text-blue-700 hover:underline"" @click.prevent=""openLinkInDrawer(scope.row.{hrefProp}, '{EscapeJs(title)}', {sizeArg})"">
                                {{{{ {textExpr} }}}}
                            </a>
                        </template>
                    ");
                return;
            }

            output.Content.SetHtmlContent($@"
                    <template #default=""scope"">
                        <a :href=""scope.row.{hrefProp} || '#'""
                           target=""_blank""
                           rel=""noopener noreferrer""
                           class=""text-blue-600 hover:text-blue-700 hover:underline"">
                            {{{{ {textExpr} }}}}
                        </a>
                    </template>
                ");
        }

        /// <summary>解析客户端字段名</summary>
        private string ResolveClientProp(string prop, ModelExpression expr)
        {
            if (!string.IsNullOrWhiteSpace(prop))
                return NormalizeProp(prop);
            if (expr == null)
                return string.Empty;
            return NormalizeProp(expr.Metadata.PropertyName ?? expr.Name);
        }

        /// <summary>标准化字段名</summary>
        private string NormalizeProp(string prop)
        {
            var name = prop ?? string.Empty;
            if (name.Contains("."))
                name = name[(name.LastIndexOf('.') + 1)..];
            if (!string.IsNullOrEmpty(name) && char.IsUpper(name[0]))
                name = char.ToLowerInvariant(name[0]) + name[1..];
            return name;
        }

        /// <summary>转义 JS 字符串</summary>
        private static string EscapeJs(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
