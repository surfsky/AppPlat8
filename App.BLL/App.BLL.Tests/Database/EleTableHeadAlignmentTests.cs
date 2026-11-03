using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using App.EleUI;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class EleTableHeadAlignmentTests
    {
        [DataTestMethod]
        [DataRow(TableHeadHAlign.Left, TableHeadVAlign.Top, "left", "top")]
        [DataRow(TableHeadHAlign.Center, TableHeadVAlign.Middle, "center", "middle")]
        [DataRow(TableHeadHAlign.Right, TableHeadVAlign.Bottom, "right", "bottom")]
        public void TableAlignmentIsAppliedToColumnAndHeaderCss(
            TableHeadHAlign horizontal, TableHeadVAlign vertical, string expectedHorizontal, string expectedVertical)
        {
            var table = new EleTable { HeadHAlign = horizontal, HeadVAlign = vertical, HeadWrap = true };
            var context = new TagHelperContext(new TagHelperAttributeList(), new Dictionary<object, object>(), "test");
            table.Init(context);

            var column = new EleNumColumn { Align = "right" };
            var output = new TagHelperOutput("EleNumColumn", new TagHelperAttributeList(),
                (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
            column.Process(context, output);

            Assert.AreEqual(expectedHorizontal, output.Attributes["header-align"].Value);
            Assert.AreEqual("right", output.Attributes["align"].Value);

            var method = typeof(EleTable).GetMethod("CreateHeaderScopeCss", BindingFlags.Instance | BindingFlags.NonPublic);
            var css = (string)method.Invoke(table, new object[] { "test-table" });
            StringAssert.Contains(css, $"text-align: {expectedHorizontal} !important");
            StringAssert.Contains(css, $"vertical-align: {expectedVertical} !important");
            StringAssert.Contains(css, "white-space: normal !important");
        }

        [TestMethod]
        public void TableDefaultsToCenteredHeadersWithoutWrapping()
        {
            var table = new EleTable();
            Assert.AreEqual(TableHeadHAlign.Center, table.HeadHAlign);
            Assert.AreEqual(TableHeadVAlign.Middle, table.HeadVAlign);
            Assert.IsFalse(table.HeadWrap);
            Assert.IsNull(typeof(EleColumn).GetProperty("HAlign"));
        }
    }
}