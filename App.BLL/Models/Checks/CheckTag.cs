using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;


/**
检查标签（CheckTag） --(n:n)-- 检查表（CheckSheet）--(1:n)-- 检查项（CheckSheetItem）
*/
namespace App.DAL
{
    [UI("检查", "标签")]
    public class CheckTag : TreeEntity<CheckTag>
    {
        [UI("组织")]        public long? OrgId { get; set; }
        [UI("组织")]        public virtual Org Org { get; set; }
        [UI("组织名称"), NotMapped]      public string OrgName => Org?.Name;

        // 检查表Id列表
        [UI("检查表Id列表"), NotMapped]   public List<long> SheetIds { get; set; } = new List<long>();      // 冗余属性，用于表单数据传递
        [UI("检查表列表")]                public virtual List<CheckSheet> Sheets { get; set; } = new List<CheckSheet>();  // 检查表。n:n关系

        //
        public override CheckTag Clone()
        {
            return base.Clone().Let(t => {
                t.OrgId = this.OrgId;
                t.SheetIds = this.SheetIds;
            });
        }

        //
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                this.Id,
                this.Name,
                this.ParentId,
                this.TreeLevel,
                this.OrgId,
                this.OrgName,
                this.SheetIds,
                this.SortId,
                this.Children // Add this line to include children in JSON output
            };
        }

        //
        public new static CheckTag GetDetail(long id)
        {
            return IncludeSet.FirstOrDefault(t => t.Id == id).Let(t => {
                t.SheetIds = t.Sheets.Select(s => s.Id).ToList();
            });
        }

        //
        public IQueryable<CheckTag> Query(string name="", long? sheetId=null, long? orgId=null)
        {
            IQueryable<CheckTag> q = CheckTag.IncludeSet;
            if (name.IsNotEmpty())  q.Where(t => t.Name.Contains(name));
            if (sheetId != null)    q.Where(t => t.Sheets.Any(s => s.Id == sheetId));
            if (orgId != null)      q.Where(t => t.OrgId == orgId);
            return q;
        }
    }
}