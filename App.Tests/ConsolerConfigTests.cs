using System;
using System.Collections.Generic;
using System.IO;
using App.Consoler;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace App.Tests;

[TestClass]
public class ConsolerConfigTests
{
    [TestMethod]
    public void Parse_LoadsFileAndAppliesCommandLineOverrides()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                {"apiBase":"http://file.example", "conn":"Data Source=relative.db", "amapKey":"file-key", "extras":{"Region":"file"}}
                """);
            var config = ConsolerConfig.Parse(new[]
            {
                "--api-base=https://cli.example", "--amap-key=cli-key", "--run=StatJob",
                "--region=cli", "--conn=Data Source=ignored.db"
            }, path);

            Assert.AreEqual("https://cli.example", config.ApiBase);
            Assert.AreEqual("cli-key", config.AmapKey);
            Assert.AreEqual("StatJob", config.RunJob);
            Assert.AreEqual("cli", config.Extras["REGION"]);
            StringAssert.Contains(config.ConnStr, Path.Combine(AppContext.BaseDirectory, "relative.db"));
            Assert.ThrowsException<NotSupportedException>(() =>
                ((IDictionary<string, string>)config.Extras)["region"] = "changed");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Parse_MissingFileUsesDefaults()
    {
        var config = ConsolerConfig.Parse(Array.Empty<string>(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()));
        Assert.AreEqual(ConsolerConfig.DefaultApiBase, config.ApiBase);
        Assert.AreEqual(string.Empty, config.ConnStr);
    }

    [TestMethod]
    public void Initialize_PublishesConfigOnlyOnce()
    {
        var config = ConsolerConfig.Initialize(new[] { "--api-base=https://singleton.example", "--test-arg=value" });
        Assert.AreSame(config, ConsolerConfig.Instance);
        Assert.AreEqual("https://singleton.example", ConsolerConfig.Instance.ApiBase);
        Assert.AreEqual("value", ConsolerConfig.Instance.Extras["test-arg"]);
        Assert.ThrowsException<InvalidOperationException>(() => ConsolerConfig.Initialize(Array.Empty<string>()));
    }

    [DataTestMethod]
    [DataRow("http://other.example/api", "http://other.example/api")]
    [DataRow("HTTPS://other.example/api", "HTTPS://other.example/api")]
    [DataRow("/api/test?x=1", "https://site.example/api/test?x=1")]
    [DataRow("api/test", "https://site.example/api/test")]
    public void ResolveDataUrl_HandlesAbsoluteAndRelativeAddresses(string input, string expected)
    {
        Assert.AreEqual(expected, ApiChecker.ResolveDataUrl(input, "https://site.example/"));
    }

    [TestMethod]
    public void ResolveDataUrl_RejectsEmptyOrUnsupportedAddresses()
    {
        Assert.ThrowsException<ArgumentException>(() => ApiChecker.ResolveDataUrl(" ", "https://site.example"));
        Assert.ThrowsException<ArgumentException>(() => ApiChecker.ResolveDataUrl("ftp://other.example/api", "https://site.example"));
    }
}