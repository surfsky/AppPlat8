using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using App.DAL;
using App.Entities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class CheckObjectGeometryJsonTests
    {
        [TestMethod]
        public void GeometryDataJsonPreservesChineseAndEscapesSpecialCharacters()
        {
            var contact = "张三\"\\\n<script>";
            var item = new CheckObject
            {
                Id = 123,
                Name = "示例企业",
                SocialCreditId = "代码123",
                DutyMan = contact,
                Tags = new List<CheckObjectTag>
                {
                    new CheckObjectTag { Tag = new CheckTag { Name = "高风险企业", TagType = CheckTagType.Basic } }
                }
            }.ToGeometryItem();

            StringAssert.Contains(item.DataJson, "张三");
            StringAssert.Contains(item.DataJson, "高风险企业");
            Assert.IsFalse(item.DataJson.Contains("\\u5F20"));
            Assert.IsFalse(item.DataJson.Contains("<script>"));

            using var inner = JsonDocument.Parse(item.DataJson);
            Assert.AreEqual(contact, inner.RootElement.GetProperty("DutyMan").GetString());
            Assert.AreEqual("高风险企业", inner.RootElement.GetProperty("TagNames")[0].GetString());
            Assert.AreEqual("代码123", inner.RootElement.GetProperty("SocialCreditId").GetString());

            // HTTP 外层仍输出 JSON 字符串，消费方可继续再次解析 dataJson。
            var response = JsonSerializer.Serialize(new { dataJson = item.DataJson },
                new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            StringAssert.Contains(response, "张三");
            using var outer = JsonDocument.Parse(response);
            Assert.AreEqual(item.DataJson, outer.RootElement.GetProperty("dataJson").GetString());
        }
    }
}