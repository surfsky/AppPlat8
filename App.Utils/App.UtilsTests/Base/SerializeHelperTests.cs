using Microsoft.VisualStudio.TestTools.UnitTesting;
using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Utils.Tests
{
    public enum Sex
    {
        Male,
        Female,
    }
    public record User(string Name, Sex Sex){}

    [TestClass()]
    public class SerializeHelperTests
    {
        [TestMethod()]
        public void ToJsonTest()
        {
            var user = new User("kevin", Sex.Male);
            var txt = user.ToJson();
            System.Diagnostics.Trace.Write(txt);
        }
    }
}