using System;
using System.Text.Json;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database;

[TestClass]
public class GisGeometryCopyCombineTests
{
    [TestMethod]
    public void Copy_CreatesNewPointWithIncrementingName()
    {
        WithDatabase(() =>
        {
            var source = NewGeometry("区域", GeometryType.Shape, Polygon(0, 0, 1, 1));
            var firstCopy = GisGeometry.Copy(source.Id);
            var secondCopy = GisGeometry.Copy(source.Id);

            Assert.AreNotEqual(source.Id, firstCopy.Id);
            Assert.AreNotEqual(firstCopy.Id, secondCopy.Id);
            Assert.AreEqual("区域1", firstCopy.Name);
            Assert.AreEqual("区域2", secondCopy.Name);
            Assert.AreEqual(source.GeoJson, firstCopy.GeoJson);
            Assert.AreEqual(GeometryType.Shape, firstCopy.Type);
        });
    }

    [TestMethod]
    public void Combine_CreatesNewShapeAndKeepsSources()
    {
        WithDatabase(() =>
        {
            var first = NewGeometry("东区", GeometryType.Shape, Polygon(0, 0, 2, 2));
            var second = NewGeometry("西区", GeometryType.Shape, Polygon(1, 0, 3, 2));

            var merged = GisGeometry.Combine(new[] { first.Id, second.Id });

            Assert.AreNotEqual(first.Id, merged.Id);
            Assert.AreEqual("东区合并", merged.Name);
            Assert.AreEqual(GeometryType.Shape, merged.Type);
            using var geoJson = JsonDocument.Parse(merged.GeoJson);
            Assert.AreEqual("东区合并", geoJson.RootElement.GetProperty("features")[0]
                .GetProperty("properties").GetProperty("label").GetString());
            Assert.IsNotNull(GisGeometry.Get(first.Id));
            Assert.IsNotNull(GisGeometry.Get(second.Id));
        });
    }

    [TestMethod]
    public void Combine_RejectsNonShapeAndSingleSelection()
    {
        WithDatabase(() =>
        {
            var shape = NewGeometry("形状", GeometryType.Shape, Polygon(0, 0, 1, 1));
            var point = NewGeometry("点位", GeometryType.Point, Polygon(1, 1, 2, 2));

            Assert.ThrowsException<ArgumentException>(() => GisGeometry.Combine(new[] { shape.Id }));
            Assert.ThrowsException<ArgumentException>(() => GisGeometry.Combine(new[] { shape.Id, point.Id }));
        });
    }

    private static GisGeometry NewGeometry(string name, GeometryType type, string geoJson)
    {
        var item = new GisGeometry
        {
            Name = name,
            Type = type,
            GeoJson = geoJson,
            IsVisible = true
        };
        item.Save(EntityOp.New);
        return item;
    }

    private static string Polygon(double minX, double minY, double maxX, double maxY) =>
        $"{{\"type\":\"Polygon\",\"coordinates\":[[[{minX},{minY}],[{maxX},{minY}],[{maxX},{maxY}],[{minX},{maxY}],[{minX},{minY}]]]}}";

    private static void WithDatabase(Action action)
    {
        var options = new DbContextOptionsBuilder<AppPlatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new AppPlatContext(options);
        Func<DbContext> onGetDb = () => db;
        EntityConfig.Instance.OnGetDb += onGetDb;
        try
        {
            action();
        }
        finally
        {
            EntityConfig.Instance.OnGetDb -= onGetDb;
        }
    }
}