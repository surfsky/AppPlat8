using App.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace App.UtilsTests.Base
{
    [TestClass]
    public class ListHelperTests
    {
        private readonly List<int> _list1 = new() { 0, 1, 2, 3, 4, 5 };
        private readonly List<int> _list2 = new() { 4, 5, 6, 7, 8, 9 };

        [TestMethod]
        public void ToJoinStringTest()
        {
            var chars = new[] { "A", "B", "C" };
            Assert.AreEqual("ABC", chars.ToJoinString());
            Assert.AreEqual("A,B,C", chars.ToJoinString(","));
            Assert.AreEqual("A;B;C", chars.ToJoinString(";"));
        }

        [TestMethod]
        public void GetItemTest()
        {
            var dict = new Dictionary<string, string>
            {
                { "a", "va" },
                { "b", "vb" },
                { "c", "vc" },
            };

            Assert.AreEqual("va", dict.GetItem("A", true));
            Assert.AreEqual(null, dict.GetItem("A", false));
        }

        [TestMethod]
        public void IndexOfTest()
        {
            Assert.AreEqual(2, _list1.IndexOf(t => t == 2));
            Assert.AreEqual(-1, _list1.IndexOf(t => t == 99));
        }

        [TestMethod]
        public void UnionTest()
        {
            var result = _list1.Union(_list2);

            CollectionAssert.AreEqual(new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, result);
        }

        [TestMethod]
        public void SplitTest()
        {
            var list = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            var result = list.Split(t => t < 4);

            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, result.Item1);
            CollectionAssert.AreEqual(new List<int> { 4, 5, 6, 7 }, result.Item2);

            var persons = App.Utils.Tests.Person.GetPersons();
            var personSplit = persons.Split(t => t.Age < 20);
            Assert.AreEqual(persons.Count, personSplit.Item1.Count + personSplit.Item2.Count);
        }

        [TestMethod]
        public void MoveItemTest()
        {
            var list = _list1.Clone();
            var newIndex = list.MoveItem(2, 4);

            Assert.AreEqual(4, newIndex);
            CollectionAssert.AreEqual(new List<int> { 0, 1, 3, 4, 2, 5 }, list);
        }

        [TestMethod]
        public void MoveItemHeadTest()
        {
            var list = _list1.Clone();
            var newIndex = list.MoveItemHead(2);

            Assert.AreEqual(0, newIndex);
            CollectionAssert.AreEqual(new List<int> { 2, 0, 1, 3, 4, 5 }, list);
        }

        [TestMethod]
        public void MoveItemTailTest()
        {
            var list = _list1.Clone();
            var newIndex = list.MoveItemTail(2);

            Assert.AreEqual(list.Count - 1, newIndex);
            CollectionAssert.AreEqual(new List<int> { 0, 1, 3, 4, 5, 2 }, list);
        }

        [TestMethod]
        public void MoveItemUpTest()
        {
            var list = _list1.Clone();
            var newIndex = list.MoveItemUp(2);

            Assert.AreEqual(1, newIndex);
            CollectionAssert.AreEqual(new List<int> { 0, 2, 1, 3, 4, 5 }, list);
        }

        [TestMethod]
        public void MoveItemDownTest()
        {
            var list = _list1.Clone();
            var newIndex = list.MoveItemDown(2);

            Assert.AreEqual(3, newIndex);
            CollectionAssert.AreEqual(new List<int> { 0, 1, 3, 2, 4, 5 }, list);
        }

        [TestMethod]
        public void SearchTest()
        {
            var result = _list1.Search(t => t % 2 == 0);
            CollectionAssert.AreEqual(new List<int> { 0, 2, 4 }, result);
        }

        [TestMethod]
        public void EachTest()
        {
            var result = _list1.Clone().Each(t => { });
            CollectionAssert.AreEqual(_list1, result);
        }

        [TestMethod]
        public void Each2Test()
        {
            var source = new List<int> { 2, 4, 6 };
            var pairs = new List<string>();

            var result = source.Each2((current, previous) =>
            {
                pairs.Add($"{previous}-{current}");
            });

            CollectionAssert.AreEqual(source, result);
            CollectionAssert.AreEqual(new List<string> { "0-2", "2-4", "4-6" }, pairs);
        }

        [TestMethod]
        public void CastIntAndCastStringTest()
        {
            var numbers = new object[] { "1", 2, 3L };
            var strings = new object[] { 1, "2", true };

            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, numbers.CastInt());
            CollectionAssert.AreEqual(new List<string> { "1", "2", "True" }, strings.CastString());
        }

        [TestMethod]
        public void CloneTest()
        {
            var result = _list1.Clone();

            CollectionAssert.AreEqual(_list1, result);
            Assert.AreNotSame(_list1, result);
        }
    }
}
