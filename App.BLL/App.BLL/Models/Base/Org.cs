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
    /// <summary>组织等级</summary>
    public enum OrgLevel
    {
        [UI("省级")]  Province = 1,
        [UI("市级")]  City = 2,
        [UI("区县")]  District = 3,
        [UI("社区")]  Community = 4,
    }

    /// <summary>组织</summary>
    public class Org : TreeEntity<Org>
    {
        [UI("备注")] public string Remark { get; set; }
        [UI("级别")] public OrgLevel? Level { get; set; }
        [UI("是否独立主体")] public bool? IsSolo { get; set; }

        public override object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                FullName,
                Level,
                Remark,
                SortId,
                TreeLevel,
                Children
            };
        }
        
        public override Org Clone()
        {
            return base.Clone()
                .SetValue(t => t.Remark, this.Remark)
                .SetValue(t => t.Level, this.Level)
                .SetValue(t => t.IsSolo, this.IsSolo)
                ;
        }

        /// <summary>获取独立主体组织</summary>
        public Org GetSoloOrg()
        {
            return IsSolo == true ? this : GetParent().GetSoloOrg();
        }

    }
}