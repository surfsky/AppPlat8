using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{

    internal class OpItem
    {
        public string Icon { get; set; }
        public string Tooltip { get; set; }
        public bool Visible { get; set; } = true;
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
            var ops = opCtx.Items.Where(c => c.Visible && !string.IsNullOrWhiteSpace(c.Handler)).ToList();

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
            var handlerExpr = BuildHandlerExpr(op.Handler);
            if (string.IsNullOrWhiteSpace(handlerExpr))
                return string.Empty;

            var icon = string.IsNullOrWhiteSpace(op.Icon) ? GuessIcon(op.Handler) : op.Icon.Trim();
            var tooltip = string.IsNullOrWhiteSpace(op.Tooltip) ? GuessTooltip(op.Handler) : op.Tooltip.Trim();
            var iconHtml = $"<el-icon class='cursor-pointer text-blue-600 hover:text-blue-700 mr-2' v-on:click=\"{handlerExpr}\"><component :is=\"'{icon}'\"></component></el-icon>";

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
                var handlerExpr = BuildHandlerExpr(op.Handler);
                if (string.IsNullOrWhiteSpace(handlerExpr))
                    continue;

                var text = string.IsNullOrWhiteSpace(op.Text)
                    ? (string.IsNullOrWhiteSpace(op.Tooltip) ? GuessTooltip(op.Handler) : op.Tooltip)
                    : op.Text;
                if (string.IsNullOrWhiteSpace(text))
                    text = op.Handler;

                items.Append($"<el-dropdown-item v-on:click=\"{handlerExpr}\">{WebUtility.HtmlEncode(text)}</el-dropdown-item>");
            }

            if (items.Length == 0)
                return string.Empty;

            return $@"
                <el-dropdown trigger='click'>
                    <span class='inline-flex items-center cursor-pointer text-blue-600 hover:text-blue-700'>
                        更多
                        <el-icon class='ml-1'><ArrowDown /></el-icon>
                    </span>
                    <template #dropdown>
                        <el-dropdown-menu>
                            {items}
                        </el-dropdown-menu>
                    </template>
                </el-dropdown>";
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

        private static string GuessIcon(string handler)
        {
            var lower = (handler ?? string.Empty).Trim().ToLowerInvariant();
            if (lower == "edit") return "Edit";
            if (lower == "delete") return "Delete";
            if (lower == "view") return "View";
            return "Operation";
        }

        private static string GuessTooltip(string handler)
        {
            var lower = (handler ?? string.Empty).Trim().ToLowerInvariant();
            if (lower == "edit") return "编辑";
            if (lower == "delete") return "删除";
            if (lower == "view") return "详情";
            return handler ?? string.Empty;
        }
    }
}
