using App.DAL;
using App.Components;
using System;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using App.Utils; 
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace App.EleUI
{

    /// <summary>表格操作列，包含编辑、删除按钮。</summary>
    [HtmlTargetElement("EleOpColumn")]
    public class EleOpColumnTagHelper : TagHelper
    {
        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        [HtmlAttributeName("UPower")]
        public Power UPower { get; set; } = Power.Web;

        [HtmlAttributeName("DPower")]
        public Power DPower { get; set; } = Power.Web;

        [HtmlAttributeName("Width")]
        public string Width { get; set; } = "130";

        [HtmlAttributeName("Label")]
        public string Label { get; set; } = "操作";

        [HtmlAttributeName("Fixed")]
        public string Fixed { get; set; } = "right";

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "el-table-column";
            output.TagMode = TagMode.StartTagAndEndTag; // Force full closing tag
            output.Attributes.SetAttribute("label", Label);
            output.Attributes.SetAttribute("width", Width);
            output.Attributes.SetAttribute("align", "center");
            output.Attributes.SetAttribute("fixed", Fixed); // Removed fixed right

            // Build buttons html
            var buttons = "";
            
            // Edit Button
            var showEdit = Auth.CheckPower(ViewContext.HttpContext, UPower);
            if (showEdit)
            {
                buttons += @"
                    <el-icon class=""cursor-pointer text-gray-600 hover:text-blue-600 mr-2"" v-on:click=""openForm(scope.row.id)"">
                        <Edit />
                    </el-icon>";
            }

            // Delete Button
            var showDelete = Auth.CheckPower(ViewContext.HttpContext, DPower);
            if (showDelete)
            {
                 buttons += @"
                    <el-icon class=""cursor-pointer text-red-500 hover:text-red-600"" style=""color: #F56C6C"" v-on:click=""deleteSingleItem(scope.row.id)"">
                        <Delete />
                    </el-icon>";
            }

            output.Content.SetHtmlContent($@"
                <template #default=""scope"">
                    <div class=""flex items-center justify-center"">
                        {buttons}
                    </div>
                </template>
            ");

        }
    }
}
