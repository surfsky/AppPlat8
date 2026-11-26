using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using App.Components;
using App.DAL;
using DalOrg = App.DAL.Org;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NPOI.XSSF.UserModel;

namespace App.Tests;

/// <summary>
/// 导入时的「文本 → 关联表 Id」解析测试。
/// 用自定义行加载器模拟关联表，避免依赖数据库。
/// </summary>
[TestClass]
public class ExcelNameIdResolverTests
{
    private static DalOrg MakeOrg(long id, string name, string fullName, string code) =>
        new DalOrg { Id = id, Name = name, FullName = fullName, Code = code };

    private static readonly DalOrg OrgTown = MakeOrg(11, "龙港市应急管理局", "浙江省/温州市/龙港市/龙港市应急管理局", "XC");
    private static readonly DalOrg OrgDept = MakeOrg(12, "新城应消管理中心", "浙江省/温州市/龙港市/龙港市应急管理局/新城应消管理中心", "Dept");
    private static readonly DalOrg OrgFactory = MakeOrg(13, "龙港市嘉穗工艺品厂（个体工商户）", "龙港市嘉穗工艺品厂（个体工商户）", "Net");

    private static readonly Role RoleAdmin = new Role { Id = 21, Name = "管理员" };
    private static readonly Role RoleUser = new Role { Id = 22, Name = "操作员" };

    private static ExcelNameIdResolver CreateResolver(IEnumerable<DalOrg> orgs = null) =>
        new(type =>
        {
            if (type == typeof(DalOrg)) return (orgs ?? new[] { OrgTown, OrgDept, OrgFactory }).Cast<object>();
            if (type == typeof(Role)) return new object[] { RoleAdmin, RoleUser };
            return Enumerable.Empty<object>();
        });

    private static PropertyInfo Prop(Type type, string name)
    {
        var prop = type.GetProperty(name);
        Assert.IsNotNull(prop, $"{type.Name} 缺少属性 {name}");
        return prop;
    }

    [TestMethod]
    public void OrgId_填名称_应解析为Id()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "OrgId"), "龙港市应急管理局", out var value, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(11L, value);
    }

    [TestMethod]
    public void OrgId_填全称_应解析为Id()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "OrgId"),
            "浙江省/温州市/龙港市/龙港市应急管理局/新城应消管理中心", out var value, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(12L, value);
    }

    [TestMethod]
    public void OrgId_填全称末段_应解析为Id()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "OrgId"), "新城应消管理中心", out var value, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(12L, value);
    }

    [TestMethod]
    public void OrgId_填编码_应解析为Id()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "OrgId"), "XC", out var value, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(11L, value);
    }

    [TestMethod]
    public void OrgId_填写时省略括号后缀_也应解析()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "OrgId"), "龙港市嘉穗工艺品厂", out var value, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(13L, value);
    }

    [TestMethod]
    public void OrgId_同时容纳空格_应解析()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "OrgId"), "  龙港市应急管理局  ", out var value, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(11L, value);
    }

    [TestMethod]
    public void OrgId_名称不存在_应失败并给出可读提示()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "OrgId"), "不存在的组织", out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "找不到");
        StringAssert.Contains(error, "不存在的组织");
    }

    [TestMethod]
    public void OrgId_名称重名_应失败并提示改填Id()
    {
        var duplicated = new[]
        {
            MakeOrg(31, "重复组织", "重复组织", "D1"),
            MakeOrg(32, "重复组织", "重复组织", "D2")
        };
        var resolver = CreateResolver(duplicated);
        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "OrgId"), "重复组织", out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "匹配到 2 条");
    }

    [TestMethod]
    public void RoleIds_多值文本_应解析为Id列表()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(User), "RoleIds"), "管理员，操作员", out var value, out var error);

        Assert.IsTrue(ok, error);
        var ids = value as List<long>;
        Assert.IsNotNull(ids);
        CollectionAssert.AreEqual(new List<long> { 21, 22 }, ids);
    }

    [TestMethod]
    public void 非Id属性_不处理()
    {
        var resolver = CreateResolver();
        var ok = resolver.TryResolve(Prop(typeof(DalOrg), "Name"), "龙港市应急管理局", out _, out _);
        Assert.IsFalse(ok);
    }

    [TestMethod]
    public void CheckerId_按用户真实姓名匹配_并忽略已逻辑删除的用户()
    {
        // 真实场景：Users 表里有两条「钟飞全」，其中 Id=309 已逻辑删除(IsDel=1)，Id=312 有效
        var deleted = new User { Id = 309, Name = "3def93df2ea04fdc", RealName = "钟飞全", IsDel = true };
        var alive = new User { Id = 312, Name = "15059353153", RealName = "钟飞全", IsDel = false };
        var resolver = new ExcelNameIdResolver(type =>
            type == typeof(User) ? new object[] { deleted, alive } : Enumerable.Empty<object>());

        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "CheckerId"), "钟飞全", out var value, out var error);

        Assert.IsTrue(ok, error);
        Assert.AreEqual(312L, value);
    }

    [TestMethod]
    public void CheckerId_只有已删除用户时_应按找不到处理()
    {
        var deleted = new User { Id = 309, Name = "3def93df2ea04fdc", RealName = "钟飞全", IsDel = true };
        var resolver = new ExcelNameIdResolver(type =>
            type == typeof(User) ? new object[] { deleted } : Enumerable.Empty<object>());

        var ok = resolver.TryResolve(Prop(typeof(CheckObject), "CheckerId"), "钟飞全", out _, out var error);

        Assert.IsFalse(ok);
        StringAssert.Contains(error, "找不到");
    }

    [TestMethod]
    public void CheckerId_按手机号或用户名也能匹配()
    {
        var alive = new User { Id = 312, Name = "15059353153", RealName = "钟飞全", Mobile = "15059353153" };
        var resolver = new ExcelNameIdResolver(type =>
            type == typeof(User) ? new object[] { alive } : Enumerable.Empty<object>());

        Assert.IsTrue(resolver.TryResolve(Prop(typeof(CheckObject), "CheckerId"), "15059353153", out var byMobile, out _));
        Assert.AreEqual(312L, byMobile);

        Assert.IsTrue(resolver.TryResolve(Prop(typeof(CheckObject), "CheckerId"), "钟飞全", out var byName, out _));
        Assert.AreEqual(312L, byName);
    }

    [TestMethod]
    public void ExcelParser_OrgId列填名称_导入时自动解析为Id()
    {
        // 构造一个只有 OrgId 一列的 Excel，值填机构名称（不是数字）
        using var ms = new MemoryStream();
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Sheet1");
        sheet.CreateRow(0).CreateCell(0).SetCellValue("OrgId");
        sheet.CreateRow(1).CreateCell(0).SetCellValue("龙港市应急管理局");
        workbook.Write(ms, true);
        ms.Position = 0;

        var parser = new ExcelParser<CheckObject>(p => p.Name == nameof(CheckObject.OrgId))
        {
            ValueResolver = CreateResolver()
        };

        var rows = parser.Fetch(ms, "test.xlsx");

        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual(11L, rows[0].OrgId);
    }

    [TestMethod]
    public void ExcelParser_OrgId列填不存在的名称_应抛出可读错误()
    {
        using var ms = new MemoryStream();
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Sheet1");
        sheet.CreateRow(0).CreateCell(0).SetCellValue("OrgId");
        sheet.CreateRow(1).CreateCell(0).SetCellValue("查无此机构");
        workbook.Write(ms, true);
        ms.Position = 0;

        var parser = new ExcelParser<CheckObject>(p => p.Name == nameof(CheckObject.OrgId))
        {
            ValueResolver = CreateResolver()
        };

        var ex = Assert.ThrowsException<FormatException>(() => parser.Fetch(ms, "test.xlsx"));
        StringAssert.Contains(ex.Message, "找不到");
        StringAssert.Contains(ex.Message, "查无此机构");
    }
}
