using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NPOI.XSSF.UserModel;

namespace App.Tests;

[TestClass]
public class ExcelStoreTests
{
    private AppPlatContext _db;
    private Func<DbContext> _getDb;
    private string _root;
    private string _oldRoot;
    private readonly ExcelStore _store = new();

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "auto-excel-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_root);
        _oldRoot = Paths.FileRoot;
        Paths.FileRoot = _root;
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
        Paths.FileRoot = _oldRoot;
        Directory.Delete(_root, true);
    }

    private void AddAttachment(long id, string content, string name = "原始表格.xlsx")
    {
        _db.Set<Att>().Add(new Att { Id = id, Content = content, FileName = name });
        _db.SaveChanges();
    }

    [TestMethod]
    public void AttachmentIdSupportsReadQuerySaveAndDelete()
    {
        var path = Path.Combine(_root, "data.xlsx");
        using (var workbook = new XSSFWorkbook())
        {
            var sheet = workbook.CreateSheet("数据");
            sheet.CreateRow(0).CreateCell(0).SetCellValue("名称");
            sheet.CreateRow(1).CreateCell(0).SetCellValue("第一行");
            using var stream = File.Create(path);
            workbook.Write(stream, true);
        }
        AddAttachment(123, "/Files/data.xlsx");

        var sheetModel = _store.ReadSheet(123);
        Assert.AreEqual("原始表格.xlsx", sheetModel.FileName);
        Assert.AreEqual("第一行", sheetModel.Rows.Single().Cells["名称"]);
        Assert.AreEqual(1, _store.Query(123, new Dictionary<string, string> { ["名称"] = "第一行" }, 0, 20).Rows.Count);
        var added = _store.SaveRow(123, null, new Dictionary<string, string> { ["名称"] = "新行" });
        Assert.AreEqual("新行", _store.GetRow(123, added.Id).Cells["名称"]);
        _store.DeleteRow(123, added.Id);
        Assert.AreEqual(1, _store.ReadSheet(123).Rows.Count);
    }

    [TestMethod]
    public void InvalidMissingRemoteTraversalAndNonExcelAttachmentsAreRejected()
    {
        AddAttachment(1, "https://example.com/data.xlsx");
        AddAttachment(2, "/Files/../outside.xlsx");
        AddAttachment(3, "/Files/document.txt");
        AddAttachment(4, "/Files/missing.xlsx");
        foreach (var id in new[] { 123, 4 })
            Assert.ThrowsException<InvalidOperationException>(() => _store.ResolveAttachment(id));
    }

    [TestMethod]
    public void SymbolicLinkOutsideAttachmentRootIsRejected()
    {
        var link = Path.Combine(_root, "external.xlsx");
        File.CreateSymbolicLink(link, Path.Combine(Path.GetTempPath(), "outside.xlsx"));
        AddAttachment(5, "/Files/external.xlsx");
        Assert.ThrowsException<InvalidOperationException>(() => _store.ResolveAttachment(5));
    }


}