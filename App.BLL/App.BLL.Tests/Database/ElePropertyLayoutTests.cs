using System.Collections.Generic;
using System.Threading.Tasks;
using App.EleUI;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class ElePropertyLayoutTests
    {
        private static async Task<string> Render(EleProperty property)
        {
            property.Prop = "dataJson";
            var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
            var output = new TagHelperOutput("EleProperty", new TagHelperAttributeList(),
                (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
            await property.ProcessAsync(context, output);
            return output.Content.GetContent();
        }

        [TestMethod]
        public async Task DefaultHeightDoesNotReserveAnEmptyRow()
        {
            var html = await Render(new EleProperty());
            StringAssert.Contains(html, "style=\"overflow:auto;\"");
            Assert.IsFalse(html.Contains("min-height:"));
            StringAssert.Contains(html, "getPropertyRows('dataJson').length === 0");
            StringAssert.Contains(html, "v-for=\"(row, idx) in getPropertyRows('dataJson')\"");
        }

        [TestMethod]
        public async Task ExplicitHeightSettingsArePreserved()
        {
            var html = await Render(new EleProperty { MinHeight = "80px", Height = "300px" });
            StringAssert.Contains(html, "min-height:80px;");
            StringAssert.Contains(html, "max-height:300px;");
        }

        [TestMethod]
        public async Task MaximumHeightDoesNotImplyMinimumHeight()
        {
            var html = await Render(new EleProperty { Height = "300px" });
            StringAssert.Contains(html, "max-height:300px;");
            Assert.IsFalse(html.Contains("min-height:"));
        }
    }
}