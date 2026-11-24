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
        /// <summary>将实体上移一位；树实体只在同级节点中移动，普通实体在本表全体记录中移动。</summary>
        public static bool MoveUp<T>(this T entity) where T : Entity<T>, ISort, new() => Move(entity, -1);

        /// <summary>将实体下移一位；树实体只在同级节点中移动，普通实体在本表全体记录中移动。</summary>
        public static bool MoveDown<T>(this T entity) where T : Entity<T>, ISort, new() => Move(entity, 1);

        /// <summary>将实体移到同级节点最前；树实体只在同级节点中移动，普通实体在本表全体记录中移动。</summary>
        public static bool MoveFirst<T>(this T entity) where T : Entity<T>, ISort, new() => Move(entity, 0, toEnd: false);

        /// <summary>将实体移到同级节点最后；树实体只在同级节点中移动，普通实体在本表全体记录中移动。</summary>
        public static bool MoveLast<T>(this T entity) where T : Entity<T>, ISort, new() => Move(entity, 0, toEnd: true);

        private static bool Move<T>(T entity, int direction, bool toEnd = false) where T : Entity<T>, ISort, new()
        {
            ArgumentNullException.ThrowIfNull(entity);
            if (entity.Id <= 0)
                throw new ArgumentException("未保存的实体不能调整排序。", nameof(entity));

            // C# 不能仅凭泛型约束重载相同参数签名；通过 ITree<T> 自动识别树实体，
            // 因而调用方使用同一组扩展方法即可获得普通表或同级树节点的对应行为。
            var treeEntity = entity as ITree<T>;
            var rows = Entity.Db.Set<T>().ToList();

            // 筛选出当前实体的同级节点
            // 按排序号和 ID 对同级列表进行排序
            var siblings = treeEntity == null
                ? rows
                : rows.Where(row => row is ITree<T> treeRow && treeRow.ParentId == treeEntity.ParentId).ToList();
            siblings = siblings
                .OrderBy(row => row.SortId)
                .ThenBy(row => row.Id)
                .ToList();

            // 找到当前实体在同级列表中的索引
            var index = siblings.FindIndex(row => row.Id == entity.Id);
            if (index < 0)
                throw new InvalidOperationException($"排序实体 {typeof(T).Name}({entity.Id}) 不存在于当前数据集中。");

            // 计算目标索引：toEnd 为 true 移到末尾，false 移到开头，否则按 direction 偏移
            var targetIndex = toEnd ? siblings.Count - 1 : direction == 0 ? 0 : index + direction;
            if (targetIndex < 0 || targetIndex >= siblings.Count || targetIndex == index)
                return false;

            // 交换目标索引的元素与当前元素的位置
            var moved = siblings[index];
            siblings.RemoveAt(index);
            siblings.Insert(targetIndex, moved);

            // 更新排序号
            var changedRows = new List<T>();
            for (var i = 0; i < siblings.Count; i++)
            {
                var row = siblings[i];
                var sortId = checked((i + 1) * 10);
                if (row.SortId == sortId)
                    continue;

                row.SortId = sortId;
                changedRows.Add(row);
            }

            // 保存所有变更的记录
            foreach (var row in changedRows)
                row.Save();
            return true;
        }

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
        /*
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
        */

        /// <summary>智能构建树：根节点=ParentId为null或ParentId不在子集里</summary>
        public static List<T> ToTree<T>(this List<T> all) where T : ITree<T>, ISort
        {
            var ids = all.Select(t => t.Id).ToHashSet();
            return ToTree(all, null, 0, ids);
        }
        static List<T> ToTree<T>(List<T> all, long? parentId, int level, HashSet<long> ids)
            where T : ITree<T>, ISort
        {
            var list = parentId == null
                ? all.Where(c => c.ParentId == null || !ids.Contains(c.ParentId.Value)).OrderBy(m => m.SortId).ToList()
                : all.Where(c => c.ParentId == parentId).OrderBy(m => m.SortId).ToList();
            foreach (var item in list)
            {
                item.Children = ToTree(all, item.Id, level + 1, ids);
                item.TreeLevel = level;
            }
            return list;
        }

        /// <summary>从缓存树中查找上级元素</summary>
        public static ITree<T> GetAncestor<T>(this T item, List<T> all, Func<T, bool> predicate)
            where T : ITree<T>
        {
            if (predicate(item))
                return item;
            var p =  all.AsQueryable().FirstOrDefault(t => t.Id == item.ParentId);
            return (p != null) ? GetAncestor(p, all, predicate) : null;
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