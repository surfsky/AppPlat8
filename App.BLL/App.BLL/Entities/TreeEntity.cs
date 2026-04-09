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


        //
        virtual public List<T> Children { get; set; }

        [NotMapped]      public int TreeLevel { get; set; }


        //-------------------------------------------------------
        // Functions
        //-------------------------------------------------------
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
                TreeLevel = TreeLevel,
                Children = this.Children?.Select(c => (T)c.Clone()).ToList()
            };
        }
    }
}