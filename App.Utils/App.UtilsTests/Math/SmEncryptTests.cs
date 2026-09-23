using Microsoft.VisualStudio.TestTools.UnitTesting;
using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Utils.Tests
{
    [TestClass()]
    public class SmEncryptHelperTests
    {
        [TestMethod()]
        public void SmTest()
        {
            var input = "原始文本";

            // 1. SM2 加密/解密（非对称，适合小数据量）
            var (pub, pri) = SmEncryptHelper.Sm2CreateKeyPair();
            Console.WriteLine($"Public Key: {pub}");
            Console.WriteLine($"Private Key: {pri}");
            string cipher = SmEncryptHelper.Sm2Encrypt(input, pub);
            string plain = SmEncryptHelper.Sm2Decrypt(cipher, pri);
            Assert.AreEqual(input, plain);

            // 2. SM4 加密/解密（对称，适合大数据量）
            string key = SmEncryptHelper.Sm4CreateKey();
            Console.WriteLine($"SM4 Key: {key}");
            string e4 = SmEncryptHelper.Sm4Encrypt(input, key);
            string d4 = SmEncryptHelper.Sm4Decrypt(e4, key);
            Assert.AreEqual(input, d4);
        }
    }
}