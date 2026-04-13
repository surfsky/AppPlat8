using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using App.Components;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    /// <summary>
    /// 列表上下文（当前仅包含条目模板）
    /// </summary>
    public class ListContext
    {
        public StringBuilder ItemTemplateHtml { get; set; } = new StringBuilder();
    }

    /// <summary>
    /// 列表控件，支持下拉滚动分页加载。
    /// </summary>
    [HtmlTargetElement("EleList")]
    [RestrictChildren("ItemTemplate")]
    public class EleListTagHelper : EleControlTagHelper
    {
        [HtmlAttributeName("Title")]
        public string Title { get; set; } = "列表";

        [HtmlAttributeName("DataHandler")]
        public string DataHandler { get; set; } = "?handler=Data";

        [HtmlAttributeName("SortField")]
        public string SortField { get; set; } = "Id";

        [HtmlAttributeName("SortDirection")]
        public string SortDirection { get; set; } = "DESC";

        [HtmlAttributeName("PageSize")]
        public int? PageSize { get; set; }

        [HtmlAttributeName("ItemClass")]
        public string ItemClass { get; set; } = "p-4 border border-gray-100 rounded-lg bg-white shadow-sm";

        [HtmlAttributeName("EmptyText")]
        public string EmptyText { get; set; } = "暂无数据";

        public override void Init(TagHelperContext context)
        {
            base.Init(context);
            context.Items[typeof(ListContext)] = new ListContext();
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            output.TagName = "div";
            AddCommonAttributes(context, output);
            output.Attributes.SetAttribute("id", "app");
            output.Attributes.SetAttribute("class", "h-full flex flex-col overflow-hidden");

            await output.GetChildContentAsync();
            var listContext = (ListContext)context.Items[typeof(ListContext)];
            var template = listContext.ItemTemplateHtml.ToString();
            if (string.IsNullOrWhiteSpace(template))
            {
                template = "<div class='text-gray-500'>{{ item && item.id ? item.id : idx + 1 }}</div>";
            }

            var pageSize = ResolvePageSize();
            var itemClass = string.IsNullOrWhiteSpace(ItemClass) ? "p-4 border border-gray-100 rounded-lg bg-white shadow-sm" : ItemClass.Trim();
            var title = string.IsNullOrWhiteSpace(Title) ? "列表" : Title.Trim();

            output.Content.AppendHtml($@"
<div class='w-full h-full flex flex-col overflow-hidden'>
    <div class='px-2 pb-2 text-sm text-gray-500 flex items-center justify-between'>
        <span>{title}</span>
        <span>共 {{{{ total }}}} 条</span>
    </div>

    <div class='flex-1 overflow-auto px-2' ref='listScrollEl' v-on:scroll.passive='onListScroll'>
        <div class='space-y-3 pb-3'>
            <div v-for='(item, idx) in items' :key='item?.id ?? (`item_${{idx}}`)' class='{itemClass}'>
                {template}
            </div>

            <div v-if='loading' class='text-center text-gray-400 py-3'>加载中...</div>
            <div v-else-if='items.length === 0' class='text-center text-gray-400 py-8'>{EmptyText}</div>
            <div v-else-if='finished' class='text-center text-gray-400 py-3'>没有更多数据了</div>
        </div>
    </div>
</div>
");

            output.Content.AppendHtml($@"
<script>
document.addEventListener('DOMContentLoaded', function() {{
    new EleListAppBuilder().mount('#app', {{
        title: '{title}',
        dataHandler: '{DataHandler}',
        pageSize: {pageSize},
        defaultSortField: '{SortField}',
        defaultSortDirection: '{SortDirection}'
    }});
}});
</script>
");
        }

        private int ResolvePageSize()
        {
            if (PageSize.GetValueOrDefault() > 0)
                return PageSize.Value;

            var size = SiteConfig.Instance?.PageSize ?? 10;
            return size > 0 ? size : 10;
        }
    }
}