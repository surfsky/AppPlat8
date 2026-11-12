using System;
using App.DAL;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database;

[TestClass]
public class EntityMoveTests
{
    [TestMethod]
    public void MoveUpAndDown_ForRegularSortableEntity_ChangesWholeTableOrder()
    {
        WithDatabase(() =>
        {
            var first = NewRole("first", 10);
            var second = NewRole("second", 20);
            var third = NewRole("third", 30);

            second.MoveUp();
            Assert.AreEqual(10, second.SortId);
            Assert.AreEqual(20, first.SortId);
            Assert.AreEqual(30, third.SortId);

            second.MoveDown();
            Assert.AreEqual(20, second.SortId);
            Assert.AreEqual(10, first.SortId);
            Assert.AreEqual(30, third.SortId);
        });
    }

    [TestMethod]
    public void MoveUp_ForTreeEntity_OnlyReordersSiblings()
    {
        WithDatabase(() =>
        {
            var parentA = NewMenu("parent A", null, 10);
            var parentB = NewMenu("parent B", null, 20);
            var firstSibling = NewMenu("first child", parentA.Id, 10);
            var movingSibling = NewMenu("moving child", parentA.Id, 20);
            var unrelated = NewMenu("other parent child", parentB.Id, 10);

            movingSibling.MoveUp();

            Assert.AreEqual(10, movingSibling.SortId);
            Assert.AreEqual(20, firstSibling.SortId);
            Assert.AreEqual(10, unrelated.SortId);
            Assert.AreEqual(parentA.Id, movingSibling.ParentId);
        });
    }

    [TestMethod]
    public void MoveAtBoundary_LeavesSortOrderUnchanged()
    {
        WithDatabase(() =>
        {
            var first = NewRole("first", 10);
            var second = NewRole("second", 20);

            first.MoveUp();

            Assert.AreEqual(10, first.SortId);
            Assert.AreEqual(20, second.SortId);
        });
    }

    private static Role NewRole(string name, int sortId)
    {
        var item = new Role { Name = name, SortId = sortId };
        item.Save();
        return item;
    }

    private static Menu NewMenu(string name, long? parentId, int sortId)
    {
        var item = new Menu { Name = name, ParentId = parentId, SortId = sortId };
        item.Save();
        return item;
    }

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