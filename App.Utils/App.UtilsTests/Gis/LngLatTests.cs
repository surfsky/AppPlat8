using Microsoft.VisualStudio.TestTools.UnitTesting;
using App.Utils.Gis;
using System;

namespace App.UtilsTests.Gis
{
    [TestClass]
    public class LngLatTests
    {
        [TestMethod]
        [DataRow("120.1234, 30.2568", 120.1234, 30.2568)]
        [DataRow("120.1234, -30.2568", 120.1234, -30.2568)]
        [DataRow("东经 120.1234, 北纬 30.2568", 120.1234, 30.2568)]
        [DataRow("120.1234E, 30.2568N", 120.1234, 30.2568)]
        [DataRow("120°07.404′, 30°15.408′", 120.1234, 30.2568)]
        [DataRow("120°07′22″E, 30°15′24″N", 120.12277777777778, 30.256666666666668)]
        [DataRow("30.2568, 120.1234", 120.1234, 30.2568)] // 自动纠正经纬度颠倒
        public void Parse_ValidGps_ReturnsCorrectLngLat(string gps, double expectedLng, double expectedLat)
        {
            var result = LngLat.Parse(gps);
            
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedLng, result.Lng, 0.0001);
            Assert.AreEqual(expectedLat, result.Lat, 0.0001);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(null)]
        [DataRow("invalid")]
        [DataRow("120.1234")]
        public void Parse_InvalidGps_ReturnsNull(string gps)
        {
            var result = LngLat.Parse(gps);
            Assert.IsNull(result);
        }
    }
}
