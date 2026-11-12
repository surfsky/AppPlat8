using System;
using System.Text.Json;
using App.EleUI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.BLL.Tests.Database
{
    [TestClass]
    public class ElePayloadEnvelopeBindingTests
    {
        private const string StructuredPayloadJson = """
        {
          "Filter": { "FileName": "工作", "Type": null },
          "Page": { "PageIndex": 0, "PageSize": 50, "SortField": "Id", "SortDirection": "ASC" },
          "Select": { "Ids": [46, 47] },
          "Context": { "UniId": "KbMenu-224", "Url": "http://localhost:6060/Shared/Atts?uniId=KbMenu-224", "Path": "/Shared/Atts" }
        }
        """;

        [TestMethod]
        public void Deserialize_StructuredPayload_BindsFilterPageSelectionAndContext()
        {
            var env = JsonSerializer.Deserialize<ElePayload>(StructuredPayloadJson);

            Assert.IsNotNull(env, "env 永远不应该为 null");
            Assert.AreEqual(2, env.SelectedCount);
            Assert.AreEqual(46, env.FirstSelectedId);
            CollectionAssert.AreEqual(new[] { 46L, 47L }, env.SelectedIds);
            Assert.AreEqual("KbMenu-224", env.UniId);
            Assert.AreEqual("/Shared/Atts", env.Context?.Path);
            Assert.AreEqual("工作", env.FilterItems["FileName"]?.ToString());
            var p = env.Pager;
            Assert.AreEqual(0, p.PageIndex);
            Assert.AreEqual(50, p.PageSize);
            Assert.AreEqual("Id", p.SortField);
            Assert.AreEqual("ASC", p.SortDirection);
        }

        [TestMethod]
        public void Deserialize_SelectInfoOnly_UsesStructuredSelection()
        {
            var env = JsonSerializer.Deserialize<ElePayload>("""
            { "Select": { "Ids": [123] } }
            """);
            Assert.IsNotNull(env);
            Assert.AreEqual(1, env.SelectedCount);
            Assert.AreEqual(123, env.FirstSelectedId);
        }

        [TestMethod]
        public void Deserialize_LegacyFlatFields_AreNotUsedAsFallback()
        {
            var env = JsonSerializer.Deserialize<ElePayload>("""
            { "selectedIds": [123], "uniId": "legacy" }
            """);
            Assert.IsNotNull(env);
            Assert.AreEqual(0, env.SelectedCount);
            Assert.AreEqual(string.Empty, env.UniId);
        }
    }
}
