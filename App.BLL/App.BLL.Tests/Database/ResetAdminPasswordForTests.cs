using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using App.Components;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class ResetAdminPasswordForTests
    {
        [TestMethod]
        public void Generate_DbPassword_For_admin123()
        {
            var pwd = PasswordUtil.CreateDbPassword("admin123");
            Assert.IsFalse(string.IsNullOrWhiteSpace(pwd), "CreateDbPassword(admin123) 不应为空");
            Assert.AreEqual(24, Convert.FromBase64String(pwd).Length, "密码哈希应为 20 bytes SHA1 + 4 bytes salt = 24 bytes");
            Assert.IsTrue(PasswordUtil.ComparePasswords(pwd, "admin123"), "刚生成的哈希应能通过 ComparePasswords 验证");

            Console.Error.WriteLine("=== NEW_ADMIN_PASSWORD_HASH_BASE64 ===");
            Console.Error.WriteLine(pwd);
            Console.Error.WriteLine("=== END NEW_ADMIN_PASSWORD_HASH_BASE64 ===");
            TestContext?.WriteLine("NEW_ADMIN_PASSWORD_HASH_BASE64: {0}", pwd);
        }

        public TestContext TestContext { get; set; }
    }
}
