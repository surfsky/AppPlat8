using Microsoft.VisualStudio.TestTools.UnitTesting;
using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace App.Utils.Tests
{
    [TestClass()]
    public class IOTests
    {
        [TestMethod()]
        public void GetFileNameTest()
        {
            string url = "http://oa.wzcc.com/oamain.aspx?a=x&b=xx";
            var name = url.GetFileName();
            var ext = url.GetFileExtension();
            var folder = url.GetFileFolder();
            var q = url.GetQuery().ToString();
            var u = url.TrimQuery();
        }

        [TestMethod()]
        public void GetNextNameTest()
        {
            var name1 = "c:\\folder\\filename.doc?x=1";

            //
            var name2 = name1.GetNextName(@"_{0}").GetNextName(@"_{0}");
            Assert.AreEqual(name2, "c:\\folder\\filename_3.doc?x=1");

            //
            var name3 = name1.GetNextName(@"-{0}").GetNextName(@"-{0}");
            Assert.AreEqual(name3, "c:\\folder\\filename-3.doc?x=1");

            //
            var name4 = name1.GetNextName(@"({0})").GetNextName(@"({0})");
            Assert.AreEqual(name4, "c:\\folder\\filename(3).doc?x=1");
        }

        [TestMethod()]
        public void GetCacheTest()
        {
            var name1 = Cacher.Get("Name", () => "Kevin");
            var name2 = Cacher.Get<string>("Name");
            Assert.AreEqual(name1, name2);

            Cacher.Set("Name", "John");
            var name3 = Cacher.Get<string>("Name");
            Assert.AreEqual(name3, "John");

            var p1 = Cacher.Get<Person>("Kevin", () => new Person("Kevin"));
            var p2 = Cacher.Get<Person>("Cherry", () => new Person("Cherry"));
            var p3 = Cacher.Get<Person>("Kevin");
            var p4 = Cacher.Get<Person>("Jerry");
            Assert.AreEqual(p1.Name, p3.Name);
            Assert.AreEqual(p4, null);
        }


        [TestMethod()]
        public void GetMimeTypeTest()
        {
            var url1 = "xxx.jpg?test=x";
            var url2 = "/a/b/xxx.doc?test=x";
            var url3 = "/a/b/xxx.xxx?test=x";
            var url4 = "/a/b/xxx?test=x";

            Assert.AreEqual(url1.GetMimeType(), @"image/jpeg");
            Assert.AreEqual(url2.GetMimeType(), @"application/msword");
            Assert.AreEqual(url3.GetMimeType(), @"application/octet-stream");
            Assert.AreEqual(url4.GetMimeType(), @"application/octet-stream");
        }

        [TestMethod()]
        public void GetFileFolderTest()
        {
            var url1 = @"xxx.jpg?test=x";
            var url2 = @"a/b/xxx.jpg?test=x";
            var url3 = @"a\b\xxx.jpg?test=x";
            Assert.AreEqual(url1.GetFileFolder(), "");
            Assert.AreEqual(url2.GetFileFolder(), @"a/b");
            Assert.AreEqual(url3.GetFileFolder(), @"a\b");
        }

        [TestMethod()]
        public void GetFileExtensionTest()
        {
            var url1 = @"xxx.jpg?test=x";
            var url2 = @"a/b/xxx.Jpg?test=x";
            var url3 = @"a\b\xxx?test=x";
            Assert.AreEqual(url1.GetFileExtension(), @".jpg");
            Assert.AreEqual(url2.GetFileExtension(), @".jpg");
            Assert.AreEqual(url3.GetFileExtension(), @"");
        }

        [TestMethod()]
        public void GetAppSettingTest()
        {
            var o1 = IO.GetAppSetting<int?>("MachineID");
            var o2 = IO.GetAppSetting<int?>("machineID");
            var o3 = IO.GetAppSetting<int?>("NotExist");
            Assert.AreEqual(o1, o2);
            Assert.AreEqual(o3, null);
        }

        [TestMethod()]
        public void IPsTest()
        {
            var ips = Net.IPs;
            ips.ForEach(t => IO.Trace(t));
        }

        [TestMethod()]
        public void ToRelativePathTest()
        {
            var sep = Path.DirectorySeparatorChar;
            var root = $"c:{sep}";
            var path = $"c:{sep}test{sep}";
            var expected = $"{sep}test{sep}";

            Assert.AreEqual(path.ToRelativePath(root), expected);
            Assert.AreEqual($"d:{sep}test{sep}".ToRelativePath(root), @"");
        }

        [TestMethod()]
        public void CombinePathTest()
        {
            var sep = System.IO.Path.DirectorySeparatorChar.ToString();
            Assert.AreEqual(@"".CombinePath(@"index.aspx"), @"index.aspx");
            // The extension method CombinePath handles separators, it doesn't necessarily produce double separators or clean them if they are part of the input in a specific way unless implemented so.
            // Let's check implementation of CombinePath. Assuming it does Path.Combine-like behavior.
            // If the input already has a separator at end of first part or start of second, it should be handled.
            
            // Testing expected behavior on current platform (macOS/Linux: /, Windows: \)
            var p1 = sep + "Admins";
            var p2 = "index.aspx";
            var expected = Path.Combine(p1, p2); 
            Assert.AreEqual(p1.CombinePath(p2), expected);
            
            p1 = sep + "Admins" + sep;
            expected = Path.Combine(p1, p2);
            // On some platforms Path.Combine might strip one separator or not add one if present.
            // But let's trust the method under test behaves like Path.Combine or better.
            // Actually, let's fix the test to match what we expect from a correct CombinePath implementation
            
            // Adjusting test to use Path.Combine as truth or constructing expected string correctly
            Assert.AreEqual((sep + "Admins" + sep).CombinePath("index.aspx"), sep + "Admins" + sep + "index.aspx");
             
             // Wait, the previous failure was: 
             // Expected:</Adminsindex.aspx>. Actual:</Admins/index.aspx>.
             // This implies (sep + "Admins" + sep).CombinePath(...) produced /Admins/index.aspx
             // But the test expected /Adminsindex.aspx ??
             // Ah, look at the previous code:
             // Assert.AreEqual(sep + @"Admins" + sep.CombinePath(@"index.aspx"), sep + @"Admins" + sep + @"index.aspx");
             // Operator precedence! 
             // sep + @"Admins" + sep.CombinePath(...) 
             // is evaluated as: sep + "Admins" + (sep.CombinePath(...))
             // sep is "/"
             // "/".CombinePath("index.aspx") -> "/index.aspx" (if logic is right)
             // So it became "/Admins" + "/index.aspx" = "/Admins/index.aspx"
             // But expected was: sep + "Admins" + sep + "index.aspx" = "/Admins/index.aspx"
             
             // Wait, let's look at the failure again:
             // Expected:</Adminsindex.aspx>. Actual:</Admins/index.aspx>.
             // This means expected was missing a slash.
             // Code was: Assert.AreEqual(sep + @"Admins" + sep.CombinePath(@"index.aspx"), sep + @"Admins" + sep + @"index.aspx");
             // The second arg (expected) is "/Admins/index.aspx"
             // The first arg (actual) is sep + "Admins" + sep.CombinePath(...)
             // sep.CombinePath("index.aspx") -> likely just "index.aspx" if sep is "/" and it thinks it's combining empty + index.aspx? No, sep is "/"
             // Let's check CombinePath source code in App.Utils/IO/IO.cs to understand it.
        }

        [TestMethod()]
        public void CombineWebPathTest()
        {
            Assert.AreEqual(@"".CombineWebPath(@"index.aspx"), @"index.aspx");
            Assert.AreEqual(@"/Admins/".CombineWebPath(@"index.aspx"), @"/Admins/index.aspx");
            Assert.AreEqual(@"/Admins".CombineWebPath(@"index.aspx"), @"/Admins/index.aspx");
            Assert.AreEqual(@"/Admins/".CombineWebPath(@"index.aspx"), @"/Admins/index.aspx");
            Assert.AreEqual(@"/Admins/".CombineWebPath(@"/Test/index.aspx"), @"/Admins/Test/index.aspx");
        }

        [TestMethod()]
        public void PrepareDirectoryTest()
        {
            var sep = System.IO.Path.DirectorySeparatorChar.ToString();
            var path1 = Path.Combine(Environment.CurrentDirectory, "test1", "test.doc");
            var path2 = Path.Combine(Environment.CurrentDirectory, "test2") + sep;
            var path3 = Path.Combine(Environment.CurrentDirectory, "test3");

            IO.PrepareDirectory(path1);
            IO.PrepareDirectory(path2);
            IO.PrepareDirectory(path3);
            Assert.AreEqual(System.IO.Directory.Exists(Path.Combine(Environment.CurrentDirectory, "test1")), true);
            Assert.AreEqual(System.IO.Directory.Exists(Path.Combine(Environment.CurrentDirectory, "test2")), true);
            Assert.AreEqual(System.IO.Directory.Exists(Path.Combine(Environment.CurrentDirectory, "test3")), true);
        }

        [TestMethod()]
        public void WriteFileTest()
        {
            var path = Path.Combine(Environment.CurrentDirectory, "log.txt");
            var txt1 = "_text_";
            IO.DeleteFile(path);
            IO.DeleteFile(path);

            // 附加文件
            IO.WriteFile(path, txt1, true);
            IO.WriteFile(path, txt1, true);
            var txt3 = IO.ReadFileText(path);
            Assert.AreEqual(txt1 + txt1, txt3);

            // 新建文件
            IO.WriteFile(path, txt1, false);
            var txt2 = IO.ReadFileText(path);
            Assert.AreEqual(txt1, txt2);
        }

    }
}