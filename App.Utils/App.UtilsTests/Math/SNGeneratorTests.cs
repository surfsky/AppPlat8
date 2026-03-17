using App.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;

namespace App.Utils.Tests
{
    [TestClass()]
    public class SNGeneratorTests
    {
        [TestMethod()]
        public void GenerateTest()
        {
            var sn = SNGenerator.Generate("12345678");
            var b = SNGenerator.Validate(sn, "12345678");
            Assert.IsTrue(b);
        }
    }
}
