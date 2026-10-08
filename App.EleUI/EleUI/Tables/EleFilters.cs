using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    [HtmlTargetElement("Filters", ParentTag = "Toolbar")]
    public class EleFilters : TagHelper
    {
        [HtmlAttributeName("Icon")]
        public string Icon { get; set; } = "Filter";

        [HtmlAttributeName("Title")]
        public string Title { get; set; } = null;

        [HtmlAttributeName("DrawerTitle")]
        public string DrawerTitle { get; set; } = null;

        [HtmlAttributeName("SearchText")]
        public string SearchText { get; set; } = null;

        /// <summary>默认显示的过滤条件数量（仅桌面端生效）。
        /// - 0 或负数：不启用折叠，全部显示（兼容历史）
        /// - 正整数 N：仅默认显示前 N 个过滤条件，多余的收起，右侧显示『展开/收起』按钮</summary>
        [HtmlAttributeName("Shows")]
        public int Shows { get; set; } = 0;

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();

            var icon = string.IsNullOrWhiteSpace(Icon) ? "Filter" : Icon.Trim();
            var title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(Title) ? Texts.Current.FilterConditions : Title.Trim());
            var drawerTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(DrawerTitle) ? Texts.Current.FilterConditions : DrawerTitle.Trim());
            var searchText = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(SearchText) ? Texts.Current.Query : SearchText.Trim());
            var cancelText = WebUtility.HtmlEncode(Texts.Current.Cancel);

            int shows = Shows;
            bool useCollapse = shows > 0;
            string desktopHtml;
            if (!useCollapse)
            {
                desktopHtml = $@"
    <div class='ele-table-filters-desktop hidden md:flex md:flex-wrap md:items-start md:gap-x-4 md:gap-y-2 w-full'>
        {content}
    </div>";
            }
            else
            {
                var wrappedItems = WrapFilterChildren(content);
                var clsId = "fd_" + Math.Abs(Guid.NewGuid().GetHashCode()).ToString("x8");
                var caretHtml = "<i class='fas fa-chevron-down text-sm transition-transform' data-ele-filter-caret aria-hidden='true'></i>";
                var showN = shows.ToString();

                // 外层容器水平 flex：filters-wrap flex-1 占满，toggle 右侧不换行
                var containerClass = "ele-table-filters-desktop ele-table-filters-expandable hidden md:flex md:items-start md:gap-x-2 w-full is-collapsed";
                var wrapClass = "ele-filter-desktop-wrap flex-1 flex flex-wrap items-start gap-x-4 gap-y-2 min-w-0";
                var toggleClass = "ele-table-filters-toggle self-start inline-flex items-center gap-x-1 cursor-pointer select-none text-blue-600 hover:text-blue-700 transition md:ml-2 shrink-0 whitespace-nowrap";
                var sb = new StringBuilder();

                sb.Append("    <div ");
                AppendAttr(sb, "id", clsId);
                AppendAttr(sb, "class", containerClass);
                AppendAttr(sb, "data-ele-filter-root", clsId);
                AppendAttr(sb, "data-ele-filter-shows", showN);
                AppendAttr(sb, "data-ele-filter-expanded", "false");
                sb.AppendLine(">");

                sb.Append("        <div class='").Append(wrapClass).Append("' data-ele-filter-wrap>");
                sb.AppendLine();
                sb.Append("            ");
                sb.AppendLine(wrappedItems);
                sb.AppendLine("        </div>");

                sb.Append("        <div ");
                AppendAttr(sb, "class", toggleClass);
                AppendAttr(sb, "data-ele-filter-toggle", "");
                AppendAttr(sb, "data-ele-filter-toggle-for", clsId);
                AppendAttr(sb, "title", "展开/收起更多过滤条件");
                sb.AppendLine(">");
                sb.AppendLine("            <span class='text-sm tracking-wide' data-ele-filter-label>展开</span>");
                sb.Append("            ").Append(caretHtml).AppendLine();
                sb.AppendLine("        </div>");

                sb.AppendLine("    </div>");

                // JS：只负责切换 class 和子项 style.display，不再依赖任何 CSS nth-child
                sb.AppendLine("    <script>");
                sb.AppendLine("    (function () {");
                sb.AppendLine("        if (window.__ele_filter_collapse_inited) return;");
                sb.AppendLine("        window.__ele_filter_collapse_inited = true;");

                // 单函数：applyFilterState(root, expanded)
                sb.AppendLine("        function applyFilterState(root, expanded) {");
                sb.AppendLine("            if (!root) return;");
                sb.AppendLine("            root.classList.toggle('is-collapsed', !expanded);");
                sb.AppendLine("            root.classList.toggle('is-expanded', expanded);");
                sb.AppendLine("            root.setAttribute('data-ele-filter-expanded', expanded ? 'true' : 'false');");
                sb.AppendLine("            var label = root.querySelector('[data-ele-filter-label]');");
                sb.AppendLine("            if (label) label.textContent = expanded ? '收起' : '展开';");
                sb.AppendLine("            var caret = root.querySelector('[data-ele-filter-caret]');");
                sb.AppendLine("            if (caret) caret.style.transform = expanded ? 'rotate(180deg)' : 'rotate(0deg)';");
                // 直接控制子项显隐（不依赖 CSS nth-child）
                sb.AppendLine("            var n = parseInt(root.getAttribute('data-ele-filter-shows') || '0', 10) || 0;");
                sb.AppendLine("            var wrap = root.querySelector('[data-ele-filter-wrap]');");
                sb.AppendLine("            if (wrap) {");
                sb.AppendLine("                var items = wrap.querySelectorAll(':scope > .ele-filter-desktop-item');");
                sb.AppendLine("                for (var i = 0; i < items.length; i++) {");
                sb.AppendLine("                    var it = items[i];");
                sb.AppendLine("                    if (!expanded && i >= n) {");
                sb.AppendLine("                        it.style.display = 'none';");
                sb.AppendLine("                    } else {");
                sb.AppendLine("                        if (it.style.removeProperty) it.style.removeProperty('display');");
                sb.AppendLine("                        else it.style.display = '';");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine("            }");
                sb.AppendLine("        }");

                // 点击事件委托（capture）
                sb.AppendLine("        document.addEventListener('click', function (ev) {");
                sb.AppendLine("            var t = ev.target;");
                sb.AppendLine("            while (t && !(t instanceof Element)) t = t.parentNode;");
                sb.AppendLine("            if (!t) return;");
                sb.AppendLine("            var toggle = t.closest ? t.closest('[data-ele-filter-toggle]') : null;");
                sb.AppendLine("            if (!toggle) return;");
                sb.AppendLine("            var forId = toggle.getAttribute('data-ele-filter-toggle-for');");
                sb.AppendLine("            if (!forId) return;");
                sb.AppendLine("            var root = document.getElementById(forId);");
                sb.AppendLine("            if (!root) return;");
                sb.AppendLine("            var expanded = root.getAttribute('data-ele-filter-expanded') === 'true';");
                sb.AppendLine("            applyFilterState(root, !expanded);");
                sb.AppendLine("        }, true);");

                // 只做 2 次轻量同步（DOMContentLoaded + setTimeout 0）
                sb.AppendLine("        function applyAllOnce() {");
                sb.AppendLine("            var roots = document.querySelectorAll('[data-ele-filter-root]');");
                sb.AppendLine("            for (var i = 0; i < roots.length; i++) {");
                sb.AppendLine("                var root = roots[i];");
                sb.AppendLine("                if (!root) continue;");
                sb.AppendLine("                var hasExp = root.classList.contains('is-expanded');");
                sb.AppendLine("                var hasCol = root.classList.contains('is-collapsed');");
                sb.AppendLine("                var expanded = hasExp && !hasCol ? true : false;");
                sb.AppendLine("                applyFilterState(root, expanded);");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("        if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', applyAllOnce);");
                sb.AppendLine("        else setTimeout(applyAllOnce, 0);");

                sb.AppendLine("    })();");
                sb.AppendLine("    </script>");

                desktopHtml = sb.ToString();
            }

            output.TagName = null;
            output.Content.SetHtmlContent($@"
<div class='col-span-full ele-table-filters-block w-auto md:w-full shrink-0 flex items-center'>
{desktopHtml}

    <div class='md:hidden flex items-center mr-2'>
        <el-button type='primary' v-on:click='openFiltersDrawer' title='{title}'>
            <el-icon><component :is='""{icon}""'></component></el-icon>
        </el-button>
    </div>

    <el-drawer
        v-model='filtersDrawerVisible'
        title='{drawerTitle}'
        direction='rtl'
        size='100%'
        append-to-body
        destroy-on-close
        :with-header='true'
    >
        <div class='ele-table-filters-drawer grid gap-3 grid-cols-1'>
            {content}
        </div>

        <template #footer>
            <div class='flex items-center justify-end gap-2'>
                <el-button v-on:click='closeFiltersDrawer'>{cancelText}</el-button>
                <el-button type='primary' v-on:click='applyFiltersAndSearch'>{searchText}</el-button>
            </div>
        </template>
    </el-drawer>
</div>");
        }

        private static void AppendAttr(StringBuilder sb, string name, string value)
        {
            if (sb == null) return;
            if (string.IsNullOrEmpty(name)) return;
            sb.Append(name);
            sb.Append('=');
            if (value == null) { sb.Append("'' "); return; }
            sb.Append('\'');
            foreach (var ch in value)
            {
                if (ch == '\'') sb.Append("&#39;");
                else if (ch == '<') sb.Append("&lt;");
                else if (ch == '>') sb.Append("&gt;");
                else if (ch == '&') sb.Append("&amp;");
                else sb.Append(ch);
            }
            sb.Append("' ");
        }

        private static string WrapFilterChildren(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return string.Empty;
            try
            {
                var sb = new StringBuilder();
                int i = 0, len = content.Length;
                while (i < len)
                {
                    while (i < len && char.IsWhiteSpace(content, i)) { sb.Append(content[i]); i++; }
                    if (i + 4 <= len && content[i] == '<' && content[i + 1] == '!' && content[i + 2] == '-' && content[i + 3] == '-')
                    {
                        int end = content.IndexOf("-->", i + 4, StringComparison.Ordinal);
                        int until = end < 0 ? len : end + 3;
                        sb.Append(content, i, until - i);
                        i = until;
                        continue;
                    }
                    if (i >= len) break;
                    if (content[i] == '<')
                    {
                        int nodeStart = i;
                        bool isClosing = i + 1 < len && content[i + 1] == '/';
                        if (isClosing)
                        {
                            int end = content.IndexOf('>', i);
                            int until = end < 0 ? len : end + 1;
                            sb.Append(content, i, until - i);
                            i = until;
                            continue;
                        }
                        int nameStart = i + 1;
                        int nameEnd = nameStart;
                        while (nameEnd < len && !char.IsWhiteSpace(content, nameEnd) && content[nameEnd] != '/' && content[nameEnd] != '>')
                            nameEnd++;
                        var tagName = content.Substring(nameStart, nameEnd - nameStart);
                        int headEnd = content.IndexOf('>', nameEnd);
                        if (headEnd < 0) { sb.Append(content, nodeStart, len - nodeStart); i = len; break; }
                        bool selfClosing = headEnd - 1 >= 0 && content[headEnd - 1] == '/';
                        int nodeEnd = len;
                        if (selfClosing ||
                            string.Equals(tagName, "br", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tagName, "hr", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tagName, "input", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(tagName, "img", StringComparison.OrdinalIgnoreCase))
                        {
                            nodeEnd = headEnd + 1;
                        }
                        else
                        {
                            int depth = 1, cursor = headEnd + 1;
                            while (cursor < len && depth > 0)
                            {
                                int open = content.IndexOf('<', cursor);
                                if (open < 0) break;
                                if (open + 2 < len && content[open + 1] == '!' && content[open + 2] == '-')
                                {
                                    int cend = content.IndexOf("-->", open + 3, StringComparison.Ordinal);
                                    if (cend < 0) break;
                                    cursor = cend + 3;
                                    continue;
                                }
                                bool isClose = open + 2 <= len && content[open + 1] == '/';
                                int gt = content.IndexOf('>', open);
                                if (gt < 0) break;
                                int tnStart = open + (isClose ? 2 : 1);
                                int tnEnd = tnStart;
                                while (tnEnd <= gt && tnEnd < len && !char.IsWhiteSpace(content, tnEnd) && content[tnEnd] != '/' && content[tnEnd] != '>')
                                    tnEnd++;
                                var tName = content.Substring(tnStart, tnEnd - tnStart);
                                bool sc = gt - 1 >= 0 && content[gt - 1] == '/';
                                if (string.Equals(tName, tagName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (isClose) depth--;
                                    else if (!sc) depth++;
                                }
                                nodeEnd = gt + 1;
                                if (depth == 0) break;
                                cursor = gt + 1;
                            }
                        }
                        var outer = content.Substring(nodeStart, nodeEnd - nodeStart);
                        sb.Append("<div class='ele-filter-desktop-item inline-flex items-start shrink-0'>");
                        sb.Append(outer);
                        sb.Append("</div>");
                        i = nodeEnd;
                        continue;
                    }
                    sb.Append(content[i]);
                    i++;
                }
                return sb.ToString();
            }
            catch
            {
                return content;
            }
        }
    }
}
