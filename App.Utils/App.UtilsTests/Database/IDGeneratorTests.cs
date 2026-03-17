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
    public class IdGeneratorTests
    {
        [TestMethod()]
        public void NewGuidTest()
        {
            var id1 = IdGenerator.NewGuid("N");
            var id3 = IdGenerator.NewGuidCombo();
            var id4 = IdGenerator.NewSnowflakeId(1);
        }

        [TestMethod()]
        public  void NewComboTest()
        {
            for (int i = 0; i < 1000; i++)
            {
                string id = IdGenerator.NewGuidCombo();
                IO.Write("{0} : {1}", id, id.ToBytes().ToInt32());
            }
        }
    }
}
