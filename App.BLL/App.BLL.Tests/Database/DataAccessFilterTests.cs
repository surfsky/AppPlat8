using System.Collections.Generic;
using App.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class DataAccessFilterTests
    {
        [TestMethod]
        public void MatchScope_ShouldAllowAll_WhenAllowAllTrue()
        {
            var scope = new DataAccessScope
            {
                Enabled = true,
                AllowAll = true,
            };

            var ok = DataAccessFilter.MatchScope(99, 999, scope, new HashSet<long>());
            Assert.IsTrue(ok);
        }

        [TestMethod]
        public void MatchScope_ShouldMatchOrg_WhenOrgInScope()
        {
            var scope = new DataAccessScope
            {
                Enabled = true,
                AllowOrg = true,
                AllowOwn = false,
                UserId = 10,
            };

            var ok = DataAccessFilter.MatchScope(2, 20, scope, new HashSet<long> { 2, 3, 4 });
            Assert.IsTrue(ok);
        }

        [TestMethod]
        public void MatchScope_ShouldMatchOwn_WhenOwnerIsCurrentUser()
        {
            var scope = new DataAccessScope
            {
                Enabled = true,
                AllowOrg = false,
                AllowOwn = true,
                UserId = 10,
            };

            var ok = DataAccessFilter.MatchScope(100, 10, scope, new HashSet<long>());
            Assert.IsTrue(ok);
        }

        [TestMethod]
        public void MatchScope_ShouldUseUnion_WhenOrgAndOwnBothEnabled()
        {
            var scope = new DataAccessScope
            {
                Enabled = true,
                AllowOrg = true,
                AllowOwn = true,
                UserId = 10,
            };

            var byOrg = DataAccessFilter.MatchScope(3, 99, scope, new HashSet<long> { 3 });
            var byOwn = DataAccessFilter.MatchScope(999, 10, scope, new HashSet<long> { 3 });
            var denied = DataAccessFilter.MatchScope(999, 88, scope, new HashSet<long> { 3 });

            Assert.IsTrue(byOrg);
            Assert.IsTrue(byOwn);
            Assert.IsFalse(denied);
        }
    }
}
