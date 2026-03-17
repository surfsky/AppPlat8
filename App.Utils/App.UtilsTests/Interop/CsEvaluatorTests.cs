using Microsoft.VisualStudio.TestTools.UnitTesting;
using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Utils.Tests
{
    [TestClass()]
    public class CsEvaluatorTests
    {
        [TestMethod()]
        public void EvalTest()
        {
            var eval = new CsEvaluator();
            var b = eval.EvalBool("5 > 4");
            var d = eval.EvalDecimal("2.5");
            var dt = eval.Eval("new DateTime(2018,1,1)");
            var dt2 = eval.Eval("DateTime.Now");
        }
    }
}