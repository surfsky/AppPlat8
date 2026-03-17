using Microsoft.VisualStudio.TestTools.UnitTesting;
using App.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace App.Utils.Tests
{
    [TestClass()]
    public class ToolTests
    {
        [TestMethod()]
        public void IsEmptyTest()
        {
            // string
            string text = null;
            Assert.IsTrue(text.IsEmpty());
            Assert.IsTrue("".IsEmpty());
            Assert.IsTrue("aa".IsNotEmpty());

            // list
            List<string> arr = null;
            Assert.IsTrue(arr.IsEmpty());
            Assert.IsTrue(new List<string> { }.IsEmpty());
            Assert.IsTrue(new List<string> { "aa" }.IsNotEmpty());

            // object
            Person p = null;
            Assert.IsTrue(p.IsEmpty());
            Assert.IsTrue(new Person().IsNotEmpty());
        }

        [TestMethod()]
        public void IIFTest()
        {
            var score = 2000;
            var result = score.IIF(t => t > 1000, "High", "Low");
        }

        [TestMethod()]
        public void IndexOfTest()
        {
            var items = new string[] { "Id", "Name", "Url" };
            var n = items.IndexOf(t => t == "Name");
            Assert.AreEqual(n, 1);
        }

        [TestMethod()]
        public void GetTextTest()
        {
            Assert.AreEqual(Util.GetText("hello world"), "hello world");
            Assert.AreEqual(Util.GetText("hello {0}", "world"), "hello world");
        }

        [TestMethod()]
        public void GetResTextTest()
        {
            // 简单测试
            Assert.AreEqual("Name".GetResText(), "Name");
            Assert.AreEqual("Name".GetResText(typeof(App.UtilsTests.Properties.Resources)), "名称");

            // 全局全球化
            UtilConfig.Instance.ApplyGlobal(typeof(App.UtilsTests.Properties.Resources));
            Assert.AreEqual("Name".GetResText(), "名称");
            Assert.AreEqual("Dummy".GetResText(), "Dummy");
        }

        public class Res
        {
            public static string Name => "姓名";
            public static string Sex = "男";
        }


        [TestMethod()]
        public void AsListTest()
        {
            var a = "test";
            var list = a.AsList();
            Assert.AreEqual(list.Count, 1);

            bool? b = null;
            var list2 = b.AsList();
            Assert.AreEqual(list2.Count, 0);
        }
    }
}