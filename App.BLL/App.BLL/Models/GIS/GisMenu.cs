using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS菜单</summary>
    [UI("GIS", "GIS菜单")]
    public class GisMenu : TreeEntity<GisMenu>
    {
        [UI("责任组织")]     public long? OrgId { get; set; }
        [UI("点位数")]        public int? DataCount { get; set; }
        [UI("最后数据时间")]   public DateTime? DataDt { get; set; }

        //
        public virtual Org Org { get; set; }
        public string OrgName => Org?.Name;

        // ITree 接口
        public override GisMenu Clone()
        {
            return base.Clone().Let(t => {
                t.OrgId = this.OrgId;
                t.CreatorId = this.CreatorId;
                t.DataCount = this.DataCount;
                t.DataDt = this.DataDt;
            });
        }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                SortId,
                OrgId,
                CreatorId,
                Children,
                DataCount,
                DataDt,

                OrgName,
            };
        }


        public static IQueryable<GisMenu> Search(string name, long? creator, long? orgId, long? parentId)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())       q = q.Where(o => o.Name.Contains(name.Trim()));
            if (creator.IsNotEmpty())    q = q.Where(o => o.CreatorId == creator.Value);
            if (orgId.IsNotEmpty())      q = q.Where(o => o.OrgId == orgId.Value);
            if (parentId.IsNotEmpty())   q = q.Where(o => o.ParentId == parentId.Value);
            return q;
        }
    }
}
