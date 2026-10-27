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
    public enum CheckTagType
    {
        [UI("基础")]        Basic = 0,
        [UI("扩展")]        Spec = 1
    }

    [UI("检查", "标签")]
    public class CheckTag : TreeEntity<CheckTag>
    {
        [UI("类型")]                     public CheckTagType TagType { get; set; } = CheckTagType.Basic;
        [UI("检查表列表")]                public virtual List<CheckSheet> Sheets { get; set; } = new List<CheckSheet>();  // 检查表。n:n关系

        [NotMapped]                     public bool Disabled {get;set;}
        [UI("检查表Id列表"), NotMapped]   public List<long> SheetIds { get; set; } = new List<long>();      // 冗余属性，用于表单数据传递


        //
        public override CheckTag Clone()
        {
            return base.Clone().Let(t => {
                t.TagType = this.TagType;
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
                this.TagType,
                this.SheetIds,
                this.SortId,
                this.Disabled,
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
        public IQueryable<CheckTag> Query(string name = "", long? sheetId = null, CheckTagType? tagType = null)
        {
            IQueryable<CheckTag> q = CheckTag.IncludeSet;
            if (name.IsNotEmpty())  q = q.Where(t => t.Name.Contains(name));
            if (sheetId != null)    q = q.Where(t => t.Sheets.Any(s => s.Id == sheetId));
            if (tagType.HasValue)   q = q.Where(t => t.TagType == tagType.Value);
            return q;
        }
    }
}