using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{

    internal class OpItem
    {
        public string Icon { get; set; }
        public string Tooltip { get; set; }
        public bool Visible { get; set; } = true;
        public string Command { get; set; }
        public string Popup { get; set; }
        public string Handler { get; set; }
        public string Text { get; set; }
    }

    internal class OpColumnContext
    {
        public List<OpItem> Items { get; } = new List<OpItem>();
    }

    [HtmlTargetElement("Op", ParentTag = "EleOpColumn")]
    public class EleOpTagHelper : TagHelper
    {
        [HtmlAttributeName("Icon")]
        public string Icon { get; set; }

        [HtmlAttributeName("Tooltip")]
        public string Tooltip { get; set; }

        [HtmlAttributeName("Visible")]
        public bool Visible { get; set; } = true;

        [HtmlAttributeName("Command")]
        public string Command { get; set; }

        [HtmlAttributeName("Popup")]
        public string Popup { get; set; }

        [HtmlAttributeName("Handler")]
        public string Handler { get; set; }

        [HtmlAttributeName("Text")]
        public string Text { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (!context.Items.TryGetValue(typeof(OpColumnContext), out var val) || !(val is OpColumnContext opCtx))
            {
                opCtx = new OpColumnContext();
                context.Items[typeof(OpColumnContext)] = opCtx;
            }

            opCtx.Items.Add(new OpItem
            {
                Icon = Icon,
                Tooltip = Tooltip,
                Visible = Visible,
                Command = Command,
                Popup = Popup,
                Handler = Handler,
                Text = Text
            });

            output.SuppressOutput();
        }
    }

    /// <summary>表格操作列，包含编辑、删除按钮。</summary>
    [HtmlTargetElement("EleOpColumn")]
    [RestrictChildren("Op")]
    public class EleOpColumnTagHelper : EleColumnBaseTagHelper
    {
        [HtmlAttributeName("MaxInlineOps")]
        public int MaxInlineOps { get; set; } = 2;

        public EleOpColumnTagHelper()
        {
            Label = "操作";
            Width = "130";
            Fixed = "right";
            Align = "center";
            Sortable = false;
            Resizable = false;
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckVisible(output))
                return;

            context.Items[typeof(OpColumnContext)] = new OpColumnContext();
            await output.GetChildContentAsync();
            var opCtx = context.Items[typeof(OpColumnContext)] as OpColumnContext ?? new OpColumnContext();
            var ops = opCtx.Items.Where(c => c.Visible &&
                (!string.IsNullOrWhiteSpace(c.Command) || !string.IsNullOrWhiteSpace(c.Popup) || !string.IsNullOrWhiteSpace(c.Handler)))
                .ToList();

            SetupColumnShell(output);
            ApplyBaseColumnAttributes(output);

            var maxInline = Math.Max(0, MaxInlineOps);
            var inlineOps = ops.Take(maxInline).ToList();
            var moreOps = ops.Skip(maxInline).ToList();

            var inlineHtml = BuildInlineOpsHtml(inlineOps);
            var moreHtml = BuildMoreDropdownHtml(moreOps);

            output.Content.SetHtmlContent($@"
                <template #default=""scope"">
                    <div class=""flex items-center justify-center"">
                        {inlineHtml}
                        {moreHtml}
                    </div>
                </template>
            ");

        }

        private static string BuildInlineOpsHtml(List<OpItem> ops)
        {
            if (ops == null || ops.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var op in ops)
            {
                sb.Append(BuildInlineOpHtml(op));
            }
            return sb.ToString();
        }

        private static string BuildInlineOpHtml(OpItem op)
        {
            var clickExpr = BuildClickExpr(op);
            if (string.IsNullOrWhiteSpace(clickExpr))
                return string.Empty;

            var icon = string.IsNullOrWhiteSpace(op.Icon) ? GuessIcon(op) : op.Icon.Trim();
            var tooltip = string.IsNullOrWhiteSpace(op.Tooltip) ? GuessTooltip(op) : op.Tooltip.Trim();
            var iconHtml = $"<el-icon class='cursor-pointer text-blue-600 hover:text-blue-700 mr-2' v-on:click=\"{clickExpr}\"><component :is=\"'{icon}'\"></component></el-icon>";

            if (string.IsNullOrWhiteSpace(tooltip))
                return iconHtml;

            return $@"<el-tooltip content='{WebUtility.HtmlEncode(tooltip)}' placement='top'>{iconHtml}</el-tooltip>";
        }

        private static string BuildMoreDropdownHtml(List<OpItem> ops)
        {
            if (ops == null || ops.Count == 0)
                return string.Empty;

            var items = new StringBuilder();
            foreach (var op in ops)
            {
                var clickExpr = BuildClickExpr(op);
                if (string.IsNullOrWhiteSpace(clickExpr))
                    continue;

                var icon = string.IsNullOrWhiteSpace(op.Icon) ? GuessIcon(op) : op.Icon.Trim();

                var text = string.IsNullOrWhiteSpace(op.Text)
                    ? (string.IsNullOrWhiteSpace(op.Tooltip) ? GuessTooltip(op) : op.Tooltip)
                    : op.Text;
                if (string.IsNullOrWhiteSpace(text))
                    text = op.Command ?? op.Handler ?? op.Popup;

                items.Append($"<el-dropdown-item v-on:click=\"{clickExpr}\"><span class='inline-flex items-center'><el-icon class='mr-1'><component :is=\"'{icon}'\"></component></el-icon>{WebUtility.HtmlEncode(text)}</span></el-dropdown-item>");
            }

            if (items.Length == 0)
                return string.Empty;

            return $@"
                <el-dropdown trigger='click'>
                    <span class='inline-flex items-center cursor-pointer text-gray-500 hover:text-gray-600' title='更多操作'>
                        <span class='text-lg leading-none select-none'>⋮</span>
                    </span>
                    <template #dropdown>
                        <el-dropdown-menu>
                            {items}
                        </el-dropdown-menu>
                    </template>
                </el-dropdown>";
        }

        private static string BuildClickExpr(OpItem op)
        {
            if (op == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(op.Popup))
                return BuildPopupExpr(op.Popup, op.Text ?? op.Tooltip ?? "详情");

            if (!string.IsNullOrWhiteSpace(op.Command))
                return BuildCommandExpr(op.Command);

            return BuildHandlerExpr(op.Handler);
        }

        private static string BuildCommandExpr(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return string.Empty;

            var normalized = command.Trim();
            var lower = normalized.ToLowerInvariant();
            if (lower == "edit")
                return "openForm(scope.row.id)";
            if (lower == "delete")
                return "deleteSingleItem(scope.row.id)";
            if (lower == "view")
                return "openView(scope.row.id)";

            return $"invokeCommand('{EscapeSingleQuoted(normalized)}')";
        }

        private static string BuildPopupExpr(string popup, string title)
        {
            if (string.IsNullOrWhiteSpace(popup))
                return string.Empty;

            var p = popup.Trim();
            var urlExpr = (p.Contains("scope.") || p.Contains("+") || p.StartsWith("`"))
                ? p
                : BuildPopupUrlExpr(p);

            var t = EscapeSingleQuoted(title ?? "详情");
            return $"openDrawer({urlExpr}, '50%', 'rtl', '{t}')";
        }

        private static string BuildPopupUrlExpr(string template)
        {
            if (string.IsNullOrEmpty(template))
                return "''";

            // 支持 Popup="'/xx?id={id}&name={name}'" 风格占位符。
            // 所有占位符默认做 encodeURIComponent，避免 URL 参数污染。
            var matches = Regex.Matches(template, "\\{([A-Za-z_][A-Za-z0-9_\\.]*)\\}");
            if (matches.Count == 0)
                return $"'{EscapeSingleQuoted(template)}'";

            var sb = new StringBuilder();
            var last = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                var literal = template.Substring(last, m.Index - last);
                if (!string.IsNullOrEmpty(literal))
                {
                    if (sb.Length > 0) sb.Append(" + ");
                    sb.Append("'").Append(EscapeSingleQuoted(literal)).Append("'");
                }

                var token = m.Groups[1].Value;
                var path = token.StartsWith("scope.") ? token : $"scope.row.{token}";
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append($"encodeURIComponent((({path}) ?? '').toString())");
                last = m.Index + m.Length;
            }

            var tail = template.Substring(last);
            if (!string.IsNullOrEmpty(tail))
            {
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append("'").Append(EscapeSingleQuoted(tail)).Append("'");
            }

            return sb.Length == 0 ? "''" : sb.ToString();
        }

        private static string EscapeSingleQuoted(string text)
        {
            return (text ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
        }

        private static string BuildHandlerExpr(string handler)
        {
            if (string.IsNullOrWhiteSpace(handler))
                return string.Empty;

            var normalized = handler.Trim();
            var lower = normalized.ToLowerInvariant();
            if (lower == "edit")
                return "openForm(scope.row.id)";
            if (lower == "delete")
                return "deleteSingleItem(scope.row.id)";
            if (lower == "view")
                return "openView(scope.row.id)";

            if (normalized.Contains("(") || normalized.Contains("=>") || normalized.Contains("scope."))
                return normalized;

            return $"{normalized}(scope.row)";
        }

        private static string GuessIcon(OpItem op)
        {
            var source = op?.Command ?? op?.Handler;
            var lower = (source ?? string.Empty).Trim().ToLowerInvariant();
            if (lower == "edit") return "Edit";
            if (lower == "delete") return "Delete";
            if (lower == "view") return "View";
            if (!string.IsNullOrWhiteSpace(op?.Popup)) return "Link";
            return "Operation";
        }

        private static string GuessTooltip(OpItem op)
        {
            var source = op?.Command ?? op?.Handler;
            var lower = (source ?? string.Empty).Trim().ToLowerInvariant();
            if (lower == "edit") return "编辑";
            if (lower == "delete") return "删除";
            if (lower == "view") return "详情";
            if (!string.IsNullOrWhiteSpace(op?.Text)) return op.Text;
            return source ?? string.Empty;
        }
    }
}
