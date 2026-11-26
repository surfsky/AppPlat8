using App.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace App.BLL.Tests
{
    /// <summary>
    /// 导出过滤回归测试：实体的 virtual 属性（Id/创建时间/导航属性）和未重写 ToString 的复杂属性
    /// （Children、操作历史、附件列表等）都不应出现在 Excel 导出里。
    /// </summary>
    [TestClass()]
    public class OrgExportTests
    {
        private static string ExportOrg()
        {
            var orgs = new List<App.DAL.Org>
            {
                new App.DAL.Org
                {
                    Id = 1,
                    Name = "龙港市应急管理局",
                    FullName = "龙港市应急管理局",
                    Code = "XC",
                    SortId = 10,
                    Gps = "120.6,27.5"
                }
            };
            return ExcelHelper.ToExcelXml(orgs);
        }

        [TestMethod()]
        public void Org导出应过滤virtual与复杂类型属性()
        {
            var xml = ExportOrg();

            // 简单字段保留
            StringAssert.Contains(xml, "编码");
            StringAssert.Contains(xml, "名称");
            StringAssert.Contains(xml, "全称");
            StringAssert.Contains(xml, "龙港市应急管理局");

            // virtual 导航属性 / 复杂类型属性不导出
            Assert.IsFalse(xml.Contains("Children"));
            Assert.IsFalse(xml.Contains("操作历史"));
            Assert.IsFalse(xml.Contains("最后操作历史"));
            Assert.IsFalse(xml.Contains("所有附件"));
            Assert.IsFalse(xml.Contains("图片附件"));
            Assert.IsFalse(xml.Contains("ImageUrls"));
            Assert.IsFalse(xml.Contains("AttrUris"));
            Assert.IsFalse(xml.Contains("System.Collections.Generic"));
        }

        [TestMethod()]
        public void Org导出不排除virtual时保留Id与时间列()
        {
            var orgs = new List<App.DAL.Org>
            {
                new App.DAL.Org { Id = 1, Name = "龙港市应急管理局", FullName = "龙港市应急管理局" }
            };
            var xml = ExcelHelper.ToExcelXml(orgs, false, false);

            // Id / 创建时间 在实体基类里是 virtual，但不排除 virtual 时应作为普通列导出
            StringAssert.Contains(xml, "创建时间");
            // 复杂类型仍然被过滤
            Assert.IsFalse(xml.Contains("System.Collections.Generic"));
            Assert.IsFalse(xml.Contains("Children"));
        }
    }
}
