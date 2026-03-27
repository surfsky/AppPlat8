using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{
    [HtmlTargetElement("Filters", ParentTag = "Toolbar")]
    public class EleFiltersTagHelper : TagHelper
    {
        [HtmlAttributeName("Icon")]
        public string Icon { get; set; } = "Filter";

        [HtmlAttributeName("Title")]
        public string Title { get; set; } = "筛选";

        [HtmlAttributeName("DrawerTitle")]
        public string DrawerTitle { get; set; } = "筛选条件";

        [HtmlAttributeName("SearchText")]
        public string SearchText { get; set; } = "查询";

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();

            var icon = string.IsNullOrWhiteSpace(Icon) ? "Filter" : Icon.Trim();
            var title = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(Title) ? "筛选" : Title.Trim());
            var drawerTitle = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(DrawerTitle) ? "筛选条件" : DrawerTitle.Trim());
            var searchText = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(SearchText) ? "查询" : SearchText.Trim());

            output.TagName = null;
            output.Content.SetHtmlContent($@"
<div class='col-span-full ele-table-filters-block'>
    <div class='hidden md:grid gap-x-4 gap-y-0 grid-cols-1 md:grid-cols-2 lg:grid-cols-4'>
        {content}
    </div>

    <div class='md:hidden pb-4'>
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
        <div class='grid gap-3 grid-cols-1'>
            {content}
        </div>

        <template #footer>
            <div class='flex items-center justify-end gap-2'>
                <el-button v-on:click='closeFiltersDrawer'>取消</el-button>
                <el-button type='primary' v-on:click='applyFiltersAndSearch'>{searchText}</el-button>
            </div>
        </template>
    </el-drawer>
</div>");
        }
    }
}