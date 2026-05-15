using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Utils;
using App.Entities;

namespace App.DAL
{
    public class Org : TreeEntity<Org>
    {
        [UI("备注")] public string Remark { get; set; }
        [UI("全称")] public string FullName { get; set; }

        public override object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                Remark,
                SortId,
                TreeLevel,
                Children
            };
        }
        
        public override Org Clone()
        {
            return base.Clone().SetValue(t => t.Remark, this.Remark);
        }

        public override void BeforeSave(EntityOp op) 
        {
            this.FullName = GetFullName();
        }

        /// <summary>获取全称（包含父级）</summary>
        public string GetFullName()
        {
            if (ParentId == null)
                return Name;
            var parent = Get(ParentId.Value);
            return parent == null ? Name : parent.GetFullName() + "/" + Name;
        }

    }
}