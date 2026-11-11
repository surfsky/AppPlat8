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
public class PathTests
{
    // 测试 Path.Combine 方法
    [TestMethod]
    public void TestCombine()
    {
        var p = Path.Combine("a", "b");
        Assert.AreEqual("a/b", p);

        var p1 = "/Users/cjh/Downloads/Projects/AppPlat/AppPlat8/App/Data/Files";
        var p2 = "/Files/KbMenu/2203660809668661248.xlsx";
        var combined = Path.Combine(p1, p2);
        Assert.AreEqual("/Users/cjh/Downloads/Projects/AppPlat/AppPlat8/App/Data/Files/Files/KbMenu/2203660809668661248.xlsx", combined);
    }

}