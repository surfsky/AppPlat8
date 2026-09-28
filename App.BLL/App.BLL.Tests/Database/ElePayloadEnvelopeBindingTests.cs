using System;
using System.Collections.Generic;
using System.Text.Json;
using App.EleUI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class ElePayloadEnvelopeBindingTests
    {
        // 模拟浏览器里合并后的 POST body JSON（结构化 + 扁平同时存在）
        private const string MergedPayloadJson = """
        {
          "Select": { "Ids": [46, 47], "Count": 2, "FirstId": 46 },
          "Context": { "UniId": "KbMenu-224", "Url": "http://localhost:6060/Shared/Atts?uniId=KbMenu-224", "Path": "/Shared/Atts" },
          "selectedIds": [46, 47],
          "filters": { "FileName": "工作", "Type": null },
          "pageIndex": 0,
          "pageSize": 50,
          "sortField": "Id",
          "sortDirection": "ASC",
          "uniId": "KbMenu-224"
        }
        """;

        // 模拟旧 EleTable 默认扁平 body：不带结构化字段
        private const string FlatPayloadJson = """
        {
          "selectedIds": [99],
          "filters": {},
          "pageIndex": 1,
          "pageSize": 20,
          "sortField": "SortId",
          "sortDirection": "DESC",
          "uniId": "KbMenu-888"
        }
        """;

        // 模拟 Payload="SelectInfo" 结构化 + 扁平兜底
        private const string SelectInfoOnlyJson = """
        {
          "Select": { "Ids": [123], "Count": 1, "FirstId": 123 },
          "selectedIds": [123],
          "filters": {},
          "pageIndex": 0,
          "pageSize": 50,
          "sortField": "Id",
          "sortDirection": "ASC",
          "uniId": "KbMenu-224"
        }
        """;

        [TestMethod]
        public void Deserialize_MergedPayload_DoesNotThrow_And_BindsBothSources()
        {
            // 这里曾经抛过：
            // System.InvalidOperationException: The JSON property name for '...Rows' collides with another property.
            // 根因是 PascalCase "Rows" 经 CamelCaseNamingPolicy 转成 "rows"，与 [JsonPropertyName("rows")] 的 Rows_Flat 冲突。
            // 现在已删除 Rows/Rows_Flat/RowInfo 定义，应通过。
            var env = JsonSerializer.Deserialize<ElePayload>(MergedPayloadJson);

            Assert.IsNotNull(env, "env 永远不应该为 null");

            // 结构化 Select 优先
            Assert.AreEqual(2, env.SafeSelectedCount);
            Assert.AreEqual(46, env.SafeFirstSelectedId);
            CollectionAssert.AreEqual(new List<long> { 46, 47 }, env.SafeSelectedIds);

            // 结构化 Context 优先
            Assert.AreEqual("KbMenu-224", env.SafeUniId);
            Assert.AreEqual("/Shared/Atts", env.Context?.Path);

            // 扁平 Filters / Page
            Assert.AreEqual("工作", env.SafeFilterItems["FileName"]?.ToString());
            var p = env.SafePage;
            Assert.AreEqual(0, p.PageIndex);
            Assert.AreEqual(50, p.PageSize);
            Assert.AreEqual("Id", p.SortField);
            Assert.AreEqual("ASC", p.SortDirection);
        }

        [TestMethod]
        public void Deserialize_FlatOldPayload_StillBindsCorrectly_Through_Fallback_Properties()
        {
            var env = JsonSerializer.Deserialize<ElePayload>(FlatPayloadJson);
            Assert.IsNotNull(env);

            // Select = null，应该走扁平 selectedIds
            Assert.IsNull(env.Select);
            Assert.AreEqual(1, env.SafeSelectedCount);
            Assert.AreEqual(99, env.SafeFirstSelectedId);

            // Context = null，应该走扁平 uniId
            Assert.IsNull(env.Context);
            Assert.AreEqual("KbMenu-888", env.SafeUniId);

            var p = env.SafePage;
            Assert.AreEqual(1, p.PageIndex);
            Assert.AreEqual(20, p.PageSize);
            Assert.AreEqual("SortId", p.SortField);
            Assert.AreEqual("DESC", p.SortDirection);
        }

        [TestMethod]
        public void Deserialize_SelectInfoOnly_Matches_Button_Payload_Setting()
        {
            // 按钮：<EleButton Handler="Test" Payload="SelectInfo">测试</EleButton>
            var env = JsonSerializer.Deserialize<ElePayload>(SelectInfoOnlyJson);
            Assert.IsNotNull(env);
            Assert.IsNotNull(env.Select, "Payload=SelectInfo 时结构化 Select 必须有值");

            // 结构化字段（Select）必须存在
            Assert.AreEqual(1, env.Select!.Ids.Count);
            Assert.AreEqual(123, env.Select.Ids[0]);

            // 业务代码用 Safe* 拿：结构化优先
            Assert.AreEqual(123, env.SafeFirstSelectedId);

            // URL/扁平兜底的 uniId 也必须能拿到
            Assert.AreEqual("KbMenu-224", env.SafeUniId);
        }
    }
}
