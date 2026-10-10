using System.Collections.Generic;
using System;
using System.Text;
using System.Threading.Tasks;
using App.Components;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Net;

namespace App.EleUI
{
    /// <summary>
    /// 列表上下文（包含条目模板、底部模板）
    /// </summary>
    public class ListContext
    {
        public StringBuilder ItemTemplateHtml { get; set; } = new StringBuilder();
        public StringBuilder FooterTemplateHtml { get; set; } = new StringBuilder();
    }

    /// <summary>
    /// 列表控件，支持下拉滚动分页加载。
    /// </summary>
    [HtmlTargetElement("EleList")]
    [RestrictChildren("ItemTemplate", "Footer")]
    public class EleList : EleControl
    {
        [HtmlAttributeName("Label")]           public string Label { get; set; }
        [HtmlAttributeName("LabelWidth")]      public string LabelWidth { get; set; } = "100px";
        /// <summary>
        /// 单个列表控件的 Label 位置；默认 null（跟随父 EleForm 全局）。
        /// Left/Right/Top：单个列表覆盖父级；
        /// None：不显示 Label，也不渲染外层 el-form-item（节省一行 Label 高度）。
        /// </summary>
        [HtmlAttributeName("LabelPosition")]   public EleLabelPosition? LabelPosition { get; set; }
        [HtmlAttributeName("ColSpan")]         public int? ColSpan { get; set; }
        [HtmlAttributeName("FillRow")]         public bool FillRow { get; set; }
        [HtmlAttributeName("ShowHeader")]      public bool? ShowHeader { get; set; }
        [HtmlAttributeName("Title")]           public string Title { get; set; } = "";
        [HtmlAttributeName("DataHandler")]     public string DataHandler { get; set; } = "?handler=Data";
        [HtmlAttributeName("SortField")]       public string SortField { get; set; } = "Id";
        [HtmlAttributeName("SortDirection")]   public string SortDirection { get; set; } = "DESC";
        [HtmlAttributeName("PageSize")]        public int? PageSize { get; set; }
        [HtmlAttributeName("ItemClass")]       public string ItemClass { get; set; } = "p-4 border border-gray-100 rounded-lg bg-white shadow-sm";
        [HtmlAttributeName("ScrollClass")]     public string ScrollClass { get; set; } = "flex-1 overflow-auto px-2";
        [HtmlAttributeName("ItemsClass")]      public string ItemsClass { get; set; } = "space-y-3 pb-3";
        [HtmlAttributeName("EmptyText")]       public string EmptyText { get; set; } = null;
        [HtmlAttributeName("LoadText")]        public string LoadText { get; set; } = null;
        [HtmlAttributeName("LastText")]        public string LastText { get; set; } = null;
        [HtmlAttributeName("MinHeight")]       public string MinHeight { get; set; } = "40px";

        public override void Init(TagHelperContext context)
        {
            base.Init(context);
            context.Items[typeof(ListContext)] = new ListContext();
        }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            if (!CheckPower(output)) return;

            var appId = $"ele-list-{Guid.NewGuid():N}";
            var listKey = $"list_{Guid.NewGuid():N}";
            var inForm = context.Items.ContainsKey("IsEleForm");
            var listHeight = Height;

            output.TagName = "div";
            Height = null;
            AddCommonAttributes(context, output);
            Height = listHeight;
            output.Attributes.RemoveAll("Label");
            output.Attributes.RemoveAll("LabelWidth");
            output.Attributes.RemoveAll("ColSpan");
            output.Attributes.RemoveAll("FillRow");
            output.Attributes.RemoveAll("ShowHeader");
            if (!inForm)
                output.Attributes.SetAttribute("id", appId);
            output.Attributes.SetAttribute("data-ele-list-key", listKey);
            output.Attributes.SetAttribute("data-ele-list-handler", DataHandler);
            output.Attributes.SetAttribute("data-ele-list-page-size", ResolvePageSize().ToString());
            output.Attributes.SetAttribute("data-ele-list-sort-field", this.SortField);
            output.Attributes.SetAttribute("data-ele-list-sort-direction", ResolveSortDirection());

            var hostClass = "h-full flex flex-col overflow-hidden";
            if (inForm)
            {
                if (FillRow)
                    hostClass += " col-span-full";
                else if (ColSpan.HasValue)
                {
                    if (ColSpan >= 24) hostClass += " col-span-full";
                    else if (ColSpan >= 12) hostClass += " col-span-1 md:col-span-2 lg:col-span-2";
                    else if (ColSpan >= 6) hostClass += " col-span-1";
                }
            }
            output.Attributes.SetAttribute("class", hostClass);

            await output.GetChildContentAsync();
            var listContext = (ListContext)context.Items[typeof(ListContext)];
            var template = listContext.ItemTemplateHtml.ToString();
            if (string.IsNullOrWhiteSpace(template))
            {
                template = "<div class='text-gray-500'>{{ item && item.id ? item.id : idx + 1 }}</div>";
            }
            var footerTemplate = listContext.FooterTemplateHtml.ToString();

            var pageSize = ResolvePageSize();
            var itemClass = string.IsNullOrWhiteSpace(ItemClass) ? "p-4 border border-gray-100 rounded-lg bg-white shadow-sm" : ItemClass.Trim();
            var scrollClass = string.IsNullOrWhiteSpace(ScrollClass) ? "flex-1 overflow-auto px-2" : ScrollClass.Trim();
            var itemsClass = string.IsNullOrWhiteSpace(ItemsClass) ? "space-y-3 pb-3" : ItemsClass.Trim();
            var title = Title.Trim();
            var stateExpr = $"eleListState(`{listKey}`)";
            var showHeader = ShowHeader ?? !inForm;
            var minHeight = string.IsNullOrWhiteSpace(MinHeight) ? "40px" : MinHeight.Trim();
            var scrollStyle = string.IsNullOrWhiteSpace(listHeight)
                ? $" style='min-height:{minHeight};'"
                : $" style='min-height:{minHeight};max-height:{listHeight};'";
            var emptyStyle = $" style='min-height:{minHeight};line-height:{minHeight};padding-top:0;padding-bottom:0;'";

            var startHtml = "";
            var endHtml = "";
            if (inForm)
            {
                // 解析 LabelPosition：控件级 > 父 EleForm 全局（ViewData["EleFormLabelPosition"]）> 默认 Left
                var pos = LabelPosition;
                if (!pos.HasValue && ViewContext != null
                    && ViewContext.ViewData["EleFormLabelPosition"] is EleLabelPosition parent)
                    pos = parent;
                if (!pos.HasValue) pos = EleLabelPosition.Left;

                // LabelPosition=None → 不显示 Label，也不渲染外层 el-form-item
                if (pos.Value != EleLabelPosition.None)
                {
                    var labelText = string.IsNullOrWhiteSpace(Label) ? title : Label.Trim();
                    if (!string.IsNullOrWhiteSpace(labelText))
                    {
                        var encodedLabel = WebUtility.HtmlEncode(labelText);
                        var labelWidthAttr = string.IsNullOrWhiteSpace(LabelWidth) ? "" : $" label-width=\"{LabelWidth}\"";
                        // 单个控件级 label-position 覆盖：仅当用户在 EleList 本身上显式设置了 Left/Right/Top 才写
                        // （从父级继承来的值会全局生效，不需要在每个 el-form-item 上重复写）
                        var lpAttr = "";
                        if (LabelPosition.HasValue && LabelPosition.Value != EleLabelPosition.None)
                        {
                            lpAttr = $" label-position=\"{LabelPosition.Value.ToString().ToLowerInvariant()}\"";
                        }
                        startHtml = $"<el-form-item label=\"{encodedLabel}\"{labelWidthAttr}{lpAttr}>";
                        endHtml = "</el-form-item>";
                    }
                }
            }

            var emptyText = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(EmptyText) ? Texts.Current.EmptyData : EmptyText);
            var loadText = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(LoadText) ? Texts.Current.LoadingText : LoadText);
            var lastText = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(LastText) ? Texts.Current.NoMoreData : LastText);
            var totalPrefix = Texts.Current.TotalPrefix;
            var totalSuffix = Texts.Current.TotalSuffix;
            var headerHtml = showHeader
                ? $"<div class='px-2 pb-2 text-sm text-gray-500 flex items-center justify-between'><span>{title}</span><span>{totalPrefix} {{{{ {stateExpr}.total }}}} {totalSuffix}</span></div>"
                : string.Empty;

            output.Content.AppendHtml($@"
{startHtml}
<div class='w-full h-full flex flex-col overflow-hidden'>
    {headerHtml}

    <div class='{scrollClass}' data-ele-list-scroll='{listKey}'{scrollStyle} v-on:scroll.passive='onEleListScroll(&quot;{listKey}&quot;, $event)'>
        <div class='{itemsClass}'>
            <div v-for='(item, idx) in {stateExpr}.items' :key='item?.id ?? (`item_${{idx}}`)' class='{itemClass}'>
                {template}
            </div>

            <div v-if='{stateExpr}.loading && {stateExpr}.items.length === 0' class='text-center text-gray-400 py-3'>{loadText}</div>
            <div v-else-if='{stateExpr}.items.length === 0' class='text-center text-gray-400{(!string.IsNullOrWhiteSpace(footerTemplate) ? " py-3 px-3 flex items-center justify-between gap-3" : " py-0")}'{emptyStyle}>
                <span>{emptyText}</span>
                {(string.IsNullOrWhiteSpace(footerTemplate) ? "" : $"<div class='flex items-center gap-2'>{footerTemplate}</div>")}
            </div>

            <div v-if='{stateExpr}.items.length > 0 && {stateExpr}.finished' class='text-center text-gray-400 py-3{(string.IsNullOrWhiteSpace(footerTemplate) ? "" : " px-3 flex items-center justify-between gap-3")}'>
                {(string.IsNullOrWhiteSpace(footerTemplate) ? lastText : $"<span>{lastText}</span><div class='flex items-center gap-2'>{footerTemplate}</div>")}
            </div>
        </div>
    </div>

    {(string.IsNullOrWhiteSpace(footerTemplate) ? "" : $@"
    <div v-if='{stateExpr}.items.length > 0 && !{stateExpr}.finished' class='pt-2 px-3 flex items-center justify-end gap-2 shrink-0 border-t border-gray-50'>
        <div class='flex items-center gap-2'>
            {footerTemplate}
        </div>
    </div>")}
</div>
{endHtml}
");

            if (inForm)
                return;

            output.Content.AppendHtml($@"
<script>
document.addEventListener('DOMContentLoaded', function() {{
    new EleListAppBuilder().mount('#{appId}', {{
        title: '{title}',
        dataHandler: '{DataHandler}',
        pageSize: {pageSize},
        defaultSortField: '{this.SortField}',
        defaultSortDirection: '{ResolveSortDirection()}'
    }});
}});
</script>
");
        }

        private int ResolvePageSize()
        {
            if (PageSize.GetValueOrDefault() > 0)
                return PageSize.Value;

            return 10;
        }

        /// <summary>解析排序方向</summary>
        private string ResolveSortDirection()
        {
            var dir = string.IsNullOrWhiteSpace(SortDirection) ? "DESC" : SortDirection.Trim().ToUpperInvariant();
            return dir == "ASC" ? "ASC" : "DESC";
        }
    }
}
