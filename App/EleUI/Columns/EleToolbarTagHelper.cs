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
    //-----------------------------------------------------------------
    // Toolbar
    //-----------------------------------------------------------------
    [HtmlTargetElement("Toolbar", ParentTag = "EleTable")]
    public class EleToolbarTagHelper : TagHelper
    {
        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            var childContent = await output.GetChildContentAsync();
            var content = childContent.GetContent();
            var wrapper = $@"
        <div class=""w-full bg-white px-5 pt-5 pb-0 grid gap-x-4 gap-y-0 grid-cols-1 md:grid-cols-2 lg:grid-cols-4"">
            {content}
        </div>
            ";
            
            var tableContext = (TableContext)context.Items[typeof(TableContext)];
            tableContext.ToolbarHtml.Append(wrapper);
            output.SuppressOutput();
        }
    }
}
