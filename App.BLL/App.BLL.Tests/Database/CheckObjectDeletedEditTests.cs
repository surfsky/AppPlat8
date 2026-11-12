using System;
using App.DAL;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database;

[TestClass]
public class CheckObjectDeletedEditTests
{
    [TestMethod]
    public void GetDetailIncludingDeleted_LoadsInvalidatedObjectForEditing()
    {
        var options = new DbContextOptionsBuilder<AppPlatContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var db = new AppPlatContext(options);
        Func<DbContext> onGetDb = () => db;
        EntityConfig.Instance.OnGetDb += onGetDb;

        try
        {
            var item = new CheckObject
            {
                Name = "已失效测试对象",
                Address = "测试地址",
                IsDel = true
            };
            item.Save();

            Assert.IsNull(CheckObject.GetDetail(item.Id), "常规详情查询仍应排除逻辑失效记录");

            var editItem = CheckObject.GetDetailIncludingDeleted(item.Id);
            Assert.IsNotNull(editItem);
            Assert.AreEqual("已失效测试对象", editItem.Name);
            Assert.AreEqual("测试地址", editItem.Address);
            Assert.IsTrue(editItem.IsDel);
        }
        finally
        {
            EntityConfig.Instance.OnGetDb -= onGetDb;
        }
    }
}