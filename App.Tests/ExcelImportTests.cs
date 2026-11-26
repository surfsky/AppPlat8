using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NPOI.XSSF.UserModel;
using DalOrg = App.DAL.Org;

namespace App.Tests;

/// <summary>
/// Excel 导入：逐行容错解析 + keyField 存在则更新
/// </summary>
[TestClass]
public class ExcelImportTests
{
    private AppPlatContext _db;
    private Func<DbContext> _getDb;

    [TestInitialize]
    public void Setup()
    {
        _db = new AppPlatContext(new DbContextOptionsBuilder<AppPlatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        _getDb = () => _db;
        EntityConfig.Instance.OnGetDb += _getDb;
    }

    [TestCleanup]
    public void Cleanup()
    {
        EntityConfig.Instance.OnGetDb -= _getDb;
        _db.Dispose();
    }

    /// <summary>构造一个 xlsx（第一行表头，其余每行一条数据）</summary>
    private static MemoryStream BuildXlsx(params string[][] rows)
    {
        var workbook = new XSSFWorkbook();
        var sheet = workbook.CreateSheet("Sheet1");
        for (var r = 0; r < rows.Length; r++)
        {
            var row = sheet.CreateRow(r);
            for (var c = 0; c < rows[r].Length; c++)
                row.CreateCell(c).SetCellValue(rows[r][c] ?? "");
        }
        var ms = new MemoryStream();
        workbook.Write(ms, true);
        ms.Position = 0;
        return ms;
    }

    //----------------------------------------------------------------
    //  解析：逐行容错
    //----------------------------------------------------------------
    [TestMethod]
    public void FetchWithErrors_单行出错不影响其它行()
    {
        using var ms = BuildXlsx(
            new[] { "Name", "Level" },
            new[] { "组织A", "国家" },
            new[] { "组织B", "不存在的级别" },   // 第3行：枚举值非法
            new[] { "组织C", "省" });

        var parser = new ExcelParser<DalOrg>(p => p.Name == "Name" || p.Name == "Level");
        var result = parser.FetchWithErrors(ms, "t.xlsx");

        Assert.AreEqual(3, result.DataRowCount, "应扫描到 3 行数据");
        Assert.AreEqual(2, result.Rows.Count, "只有 2 行可用");
        Assert.AreEqual(1, result.SkippedRowCount);

        var err = result.Errors.Single();
        Assert.AreEqual(3, err.RowNo);
        StringAssert.Contains(err.Message, "不存在的级别");

        CollectionAssert.AreEquivalent(new[] { "Name", "Level" }, result.MatchedProperties);
    }

    [TestMethod]
    public void FetchWithErrors_宽松模式_忽略坏单元格继续导入()
    {
        using var ms = BuildXlsx(
            new[] { "Name", "Level" },
            new[] { "组织A", "国家" },
            new[] { "组织B", "不存在的级别" },
            new[] { "组织C", "省" });

        var parser = new ExcelParser<DalOrg>(p => p.Name == "Name" || p.Name == "Level");
        var result = parser.FetchWithErrors(ms, "t.xlsx", lenient: true);

        Assert.AreEqual(3, result.Rows.Count, "宽松模式下 3 行都应导入");
        Assert.AreEqual(0, result.Errors.Count);
        Assert.AreEqual(1, result.Warnings.Count);
        Assert.AreEqual(3, result.Warnings[0].RowNo);

        var bad = (DalOrg)result.Rows.Single(r => r.RowNo == 3).Item;
        Assert.AreEqual("组织B", bad.Name);
        Assert.IsNull(bad.Level, "无法识别的枚举应置空");
    }

    //----------------------------------------------------------------
    //  keyField：存在则更新，不存在则新增
    //----------------------------------------------------------------
    [TestMethod]
    public void KeyField_存在则更新_不存在则新增()
    {
        _db.Set<DalOrg>().Add(new DalOrg { Id = 100, Name = "组织A", FullName = "组织A", Code = "OLD", Remark = "保留值" });
        _db.SaveChanges();

        using var ms = BuildXlsx(
            new[] { "Name", "Code" },
            new[] { "组织A", "NEW" },       // 第2行：已存在 → 更新
            new[] { "组织B", "B1" });       // 第3行：不存在 → 新增

        var result = ExcelImporter.Import(typeof(DalOrg), ms, "t.xlsx", new ExcelImportOptions { KeyField = "Name" });

        Assert.AreEqual(1, result.Updated, "组织A 应更新");
        Assert.AreEqual(1, result.Inserted, "组织B 应新增");
        Assert.AreEqual(0, result.Failed);

        var updated = _db.Set<DalOrg>().AsNoTracking().Single(t => t.Name == "组织A");
        Assert.AreEqual(100L, updated.Id, "更新时不能改变 Id");
        Assert.AreEqual("NEW", updated.Code);
        Assert.AreEqual("保留值", updated.Remark, "Excel 中不存在的列不应被覆盖");

        Assert.AreEqual(1, _db.Set<DalOrg>().AsNoTracking().Count(t => t.Name == "组织B"));

        Assert.IsTrue(result.Logs.Any(l => l.Contains("第2行") && l.Contains("更新") && l.Contains("成功") && l.Contains("组织A")));
        Assert.IsTrue(result.Logs.Any(l => l.Contains("第3行") && l.Contains("插入") && l.Contains("成功") && l.Contains("组织B")));
    }

    [TestMethod]
    public void KeyField_为空时全部新增()
    {
        using var ms = BuildXlsx(
            new[] { "Name", "Code" },
            new[] { "组织A", "A1" });

        var r1 = ExcelImporter.Import(typeof(DalOrg), ms, "t.xlsx");
        Assert.AreEqual(1, r1.Inserted);

        using var ms2 = BuildXlsx(
            new[] { "Name", "Code" },
            new[] { "组织A", "A2" });

        var r2 = ExcelImporter.Import(typeof(DalOrg), ms2, "t.xlsx");
        Assert.AreEqual(1, r2.Inserted, "未指定 keyField 时应继续新增");
        Assert.AreEqual(0, r2.Updated);
        Assert.AreEqual(2, _db.Set<DalOrg>().AsNoTracking().Count(t => t.Name == "组织A"));
    }

    [TestMethod]
    public void KeyField_不在Excel列中_提示并全部新增()
    {
        _db.Set<DalOrg>().Add(new DalOrg { Id = 200, Name = "组织X", FullName = "组织X", Code = "C1" });
        _db.SaveChanges();

        // Excel 只有 Code 列，没有 Name 列 → 无法按 Name 匹配
        using var ms = BuildXlsx(
            new[] { "Code" },
            new[] { "C1" });

        var result = ExcelImporter.Import(typeof(DalOrg), ms, "t.xlsx", new ExcelImportOptions { KeyField = "Name" });

        Assert.AreEqual(1, result.Inserted);
        Assert.AreEqual(0, result.Updated);
        Assert.IsTrue(result.Logs.Any(l => l.Contains("不在本次 Excel 的列中")));
    }

    [TestMethod]
    public void 行日志格式_包含行号操作状态与概述()
    {
        using var ms = BuildXlsx(
            new[] { "Name", "Code" },
            new[] { "温州市示例有限公司", "07-02-001" });

        var result = ExcelImporter.Import(typeof(DalOrg), ms, "t.xlsx");

        var line = result.Logs.Single(l => l.Contains("第2行"));
        StringAssert.Contains(line, "插入");
        StringAssert.Contains(line, "成功");
        StringAssert.Contains(line, "温州市示例有限公司");
        StringAssert.Contains(line, "07-02-001");
        StringAssert.Contains(line, "|");
    }
}
