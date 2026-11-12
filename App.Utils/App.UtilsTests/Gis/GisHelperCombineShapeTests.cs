using System;
using System.Linq;
using System.Text.Json;
using App.Utils.Gis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.UtilsTests.Gis;

[TestClass]
public class GisHelperCombineShapeTests
{
    [TestMethod]
    public void CombineShape_OverlappingPolygons_ReturnsUnionPolygon()
    {
        var result = GisHelper.CombineShape(new[]
        {
            "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[2,0],[2,2],[0,2],[0,0]]]} ",
            "{\"type\":\"Feature\",\"properties\":{},\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[[1,0],[3,0],[3,2],[1,2],[1,0]]]}}"
        });

        using var document = JsonDocument.Parse(result);
        var feature = document.RootElement.GetProperty("features").EnumerateArray().Single();
        Assert.AreEqual("FeatureCollection", document.RootElement.GetProperty("type").GetString());
        Assert.AreEqual("Polygon", feature.GetProperty("geometry").GetProperty("type").GetString());
        Assert.IsTrue(feature.GetProperty("geometry").GetProperty("coordinates")[0].GetArrayLength() >= 5);
    }

    [TestMethod]
    public void CombineShape_DisjointPolygons_ReturnsMultiPolygon()
    {
        var first = "{\"type\":\"FeatureCollection\",\"features\":[{\"type\":\"Feature\",\"geometry\":{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]},\"properties\":{}}]}";
        var second = "\"{\\\"type\\\":\\\"Polygon\\\",\\\"coordinates\\\":[[[3,0],[4,0],[4,1],[3,1],[3,0]]]}\"";

        var result = GisHelper.CombineShape(new[] { first, second });
        using var document = JsonDocument.Parse(result);
        var geometry = document.RootElement.GetProperty("features")[0].GetProperty("geometry");
        Assert.AreEqual("MultiPolygon", geometry.GetProperty("type").GetString());
        Assert.AreEqual(2, geometry.GetProperty("coordinates").GetArrayLength());
    }

    [TestMethod]
    public void CombineShape_RejectsFewerThanTwoPolygonRegions()
    {
        Assert.ThrowsException<ArgumentException>(() => GisHelper.CombineShape(new[]
        {
            "{\"type\":\"Polygon\",\"coordinates\":[[[0,0],[1,0],[1,1],[0,1],[0,0]]]}"
        }));
    }
}