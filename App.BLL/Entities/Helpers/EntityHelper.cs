using App.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace App.Entities
{

    /// <summary>
    /// 静态辅助方法
    /// </summary>
    public static class EntityHelper
    {
        /// <summary>导出实体列表为匿名对象列表</summary>
        public static List<object> Export<T>(this List<T> list, ExportMode type = ExportMode.Normal) where T : IExport
        {
            return list.Select(t => t.Export(type)).ToList();
        }


        /// <summary>获取实体的真实类型（而不是临时的代理类型）</summary>
        public static Type GetEntityType(this Type type)
        {
            if (type.FullName.StartsWith("System.Data.Entity.DynamicProxies"))
                return type.BaseType;
            return type;
        }

        //---------------------------------------------
        // ITree
        //---------------------------------------------
        /// <summary>构建树结构</summary>
        /// <param name="all"></param>
        /// <param name="parentId"></param>
        public static List<T> ToTree<T>(this List<T> all, long? parentId=null, int treeLevel=0) where T : ITree<T>, ISort
        {
            var list = all.Where(c => c.ParentId == parentId).OrderBy(m => m.SortId).ToList();
            foreach (var item in list)
            {
                item.Children = ToTree(all, item.Id, treeLevel + 1);
                item.TreeLevel = treeLevel;
            }
            return list;
        }

        /// <summary>递归获取子节点（包含自身）</summary>
        /// <param name="all">所有元素</param>
        /// <param name="rootId">根元素Id</param>
        /// <returns>根元素及其子孙节点列表</returns>
        public static List<T> GetDescendants<T>(this List<T> all, long? rootId)
            where T : ITree<T>
        {
            if (rootId == null) 
                return new List<T>();
            else
                return GetDescendants(all, new List<long> { rootId.Value });
        }

        /// <summary>递归获取子节点（包含自身）</summary>
        public static List<T> GetDescendants<T>(this List<T> all, List<long> rootIds)
            where T : ITree<T>
        {
            var result = new List<T>();
            foreach (var rootId in rootIds)
            {
                var items = new List<T>();
                GetDescendantsInternal(all, items, rootId);
                result = result.Union(items);
            }
            return result;
        }

        static void GetDescendantsInternal<T>(List<T> all, List<T> items, long? rootId)
            where T : ITree<T>
        {
            if (rootId == null)
                return;
            var root = all.AsQueryable().FirstOrDefault(t => t.Id == rootId);
            if (root != null)
            {
                if (!items.Contains(root))
                    items.Add(root);
                var children = all.AsQueryable().Where(t => t.ParentId == root.Id);
                foreach (var child in children)
                    GetDescendantsInternal(all, items, child.Id);
            }
        }

    }
}