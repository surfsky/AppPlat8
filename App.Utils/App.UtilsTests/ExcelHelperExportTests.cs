using App.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Reflection;

namespace App.Utils.Tests
{
    /// <summary>子对象（重写了 ToString，可作为"可导出复杂类型"）</summary>
    public class ExportTestAddress
    {
        public string City { get; set; }

        public override string ToString() => City ?? "";
    }

    /// <summary>子对象（未重写 ToString，不可导出）</summary>
    public class ExportTestChild
    {
        public string Name { get; set; }
    }

    /// <summary>导出过滤测试模型（Id 与 Entity 基类一样是 virtual）</summary>
    public class ExportTestModel
    {
        public virtual long Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }

        public virtual List<ExportTestChild> Children { get; set; }     // virtual + 复杂 → 不导出
        public List<string> Tags { get; set; }                          // 复杂且未重写 ToString → 不导出
        public ExportTestAddress Address { get; set; }                  // 复杂但重写 ToString → 导出
        public virtual ExportTestAddress VirtualAddress { get; set; }    // virtual 复杂 → 不导出
        public virtual int VirtualNumber { get; set; }                  // virtual 简单（EF 标量列）
    }

    [TestClass()]
    public class ExcelHelperExportTests
    {
        private static ExportTestModel CreateItem() => new ExportTestModel
        {
            Id = 1,
            Name = "n1",
            Age = 20,
            Children = new List<ExportTestChild> { new ExportTestChild { Name = "c1" } },
            Tags = new List<string> { "t1" },
            Address = new ExportTestAddress { City = "温州" },
            VirtualAddress = new ExportTestAddress { City = "龙港" },
            VirtualNumber = 9
        };

        [TestMethod()]
        public void IsExportableProperty_过滤virtual与复杂类型()
        {
            var type = typeof(ExportTestModel);
            //id
            Assert.IsFalse(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.Id)), true));
            Assert.IsTrue(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.Id)), false));


            //
            Assert.IsTrue(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.Name)), true));
            Assert.IsTrue(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.Address)), true));
            // 非 virtual 复杂类型且未重写 ToString
            Assert.IsFalse(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.Tags)), true));
            // virtual
            Assert.IsFalse(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.Children)), true));
            Assert.IsFalse(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.VirtualAddress)), true));
            Assert.IsFalse(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.VirtualNumber)), true));
        }

        [TestMethod()]
        public void IsExportableProperty_不排除virtual时保留标量列()
        {
            var type = typeof(ExportTestModel);
            Assert.IsTrue(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.VirtualNumber)), false));
            // 复杂类型即使不排除 virtual，也会因为未重写 ToString 被过滤
            Assert.IsFalse(ExcelHelper.IsExportableProperty(type.GetProperty(nameof(ExportTestModel.Children)), false));
        }

        [TestMethod()]
        public void ToExcelXml_默认不导出virtual与无ToString的复杂属性()
        {
            var xml = ExcelHelper.ToExcelXml(new List<ExportTestModel> { CreateItem() });

            StringAssert.Contains(xml, ">Name<");
            StringAssert.Contains(xml, ">Age<");
            StringAssert.Contains(xml, ">Address<");
            StringAssert.Contains(xml, ">温州<");

            Assert.IsFalse(xml.Contains("Children"), "virtual 复杂属性不应导出");
            Assert.IsFalse(xml.Contains("Tags"), "未重写 ToString 的复杂属性不应导出");
            Assert.IsFalse(xml.Contains("VirtualAddress"), "virtual 属性不应导出");
            Assert.IsFalse(xml.Contains("VirtualNumber"), "virtual 属性不应导出");
            Assert.IsFalse(xml.Contains("System.Collections.Generic"), "不应导出集合类型名");
            // 排除 virtual 时，Id 也是 virtual，默认不导出
            Assert.IsFalse(xml.Contains(">Id<"));
        }

        [TestMethod()]
        public void ToExcelXml_不排除virtual时保留Id等标量列()
        {
            var xml = ExcelHelper.ToExcelXml(new List<ExportTestModel> { CreateItem() }, false, false);

            StringAssert.Contains(xml, ">Id<");
            StringAssert.Contains(xml, ">VirtualNumber<");
            StringAssert.Contains(xml, ">9<");
            Assert.IsFalse(xml.Contains("Children"), "virtual 复杂属性仍不应导出");
            Assert.IsFalse(xml.Contains("System.Collections.Generic"), "不应导出集合类型名");
        }
    }
}
