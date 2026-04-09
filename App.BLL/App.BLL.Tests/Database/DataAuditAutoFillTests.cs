using System;
using App.DAL;
using App.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class DataAuditAutoFillTests
    {
        [TestMethod]
        public void SaveChanges_ShouldAutoFillCreatorOwnerOrgAndAuthor_OnInsert()
        {
            var options = new DbContextOptionsBuilder<AppPlatContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            Func<DataAuditScope> onGetAuditScope = () => new DataAuditScope
            {
                Enabled = true,
                UserId = 1001,
                OrgId = 2002,
            };

            EntityConfig.Instance.OnGetDataAuditScope += onGetAuditScope;
            try
            {
                using var db = new AppPlatContext(options);
                var item = new Announce
                {
                    Title = "test",
                    Content = "test",
                    Status = AnnounceStatus.Draft,
                };

                db.Announces.Add(item);
                db.SaveChanges();

                Assert.AreEqual(1001L, item.CreatorId);
                Assert.AreEqual(1001L, item.OwnerId);
                Assert.AreEqual(2002L, item.OrgId);
                Assert.AreEqual(1001L, item.AuthorId);
            }
            finally
            {
                EntityConfig.Instance.OnGetDataAuditScope -= onGetAuditScope;
            }
        }

        [TestMethod]
        public void SaveChanges_ShouldNotOverrideProvidedCreatorOwnerOrgAndAuthor_OnInsert()
        {
            var options = new DbContextOptionsBuilder<AppPlatContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            Func<DataAuditScope> onGetAuditScope = () => new DataAuditScope
            {
                Enabled = true,
                UserId = 1001,
                OrgId = 2002,
            };

            EntityConfig.Instance.OnGetDataAuditScope += onGetAuditScope;
            try
            {
                using var db = new AppPlatContext(options);
                var item = new Announce
                {
                    Title = "test",
                    Content = "test",
                    Status = AnnounceStatus.Draft,
                    CreatorId = 9001,
                    OwnerId = 9004,
                    OrgId = 9002,
                    AuthorId = 9003,
                };

                db.Announces.Add(item);
                db.SaveChanges();

                Assert.AreEqual(9001L, item.CreatorId);
                Assert.AreEqual(9004L, item.OwnerId);
                Assert.AreEqual(9002L, item.OrgId);
                Assert.AreEqual(9003L, item.AuthorId);
            }
            finally
            {
                EntityConfig.Instance.OnGetDataAuditScope -= onGetAuditScope;
            }
        }
    }
}
