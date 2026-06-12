using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Utils;
//using System.Data.Entity;
//using System.Data.Entity.Infrastructure;
//using EntityFramework.Extensions;

namespace App.Entities
{
    /// <summary>
    /// 树实体基类
    /// </summary>
    public class TreeEntity<T> : EntityBase<T>, ITree<T>
        where T : TreeEntity<T>, new()
    {
        [UI("上级部门")] public long? ParentId { get; set; }
        [UI("名称")] public string Name { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("全称")] public string FullName { get; set; }

        // Relations
        public virtual List<T> Children { get; set; }

        [NotMapped]      public int TreeLevel { get; set; }


        //-------------------------------------------------------
        // Functions
        //-------------------------------------------------------
        public override void BeforeSave(EntityOp op) 
        {
            this.FullName = GetFullName();
        }

        /// <summary>获取全称（包含父级）</summary>
        public string GetFullName()
        {
            if (ParentId == null)
                return Name;
            var parent = GetParent();
            return parent == null ? Name : parent.GetFullName() + "/" + Name;
        }

        /// <summary>获取父级</summary>
        public T GetParent()
        {
            return Get(ParentId);
        }

        /// <summary>获取树结构字符串</summary>
        public override string ToString()
        {
            return "  ".Repeat(this.TreeLevel) + this.Name;
        }

        /// <summary>获取树结构缓存</summary>
        public static List<T> GetTree()  => Cacher.Get(TreeCacheName, () => IncludeSet.ToList().ToTree(), DateTime.Now.AddHours(1));

        /// <summary> 克隆实体 </summary>
        public virtual T Clone()
        {
            return new T(){
                Id = Id,
                ParentId = ParentId,
                Name = Name,
                SortId = SortId,
                FullName = FullName,
                TreeLevel = TreeLevel,
                Children = this.Children?.Select(c => (T)c.Clone()).ToList()
            };
        }
    }
}