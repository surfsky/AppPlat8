using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS几何图形</summary>
    [UI("GIS", "GIS几何图形")]
    public class GisGeometry : TreeEntity<GisGeometry>
    {
        [UI("简称")]        public string Alias { get; set; }
        [UI("责任组织")]     public long? OrgId { get; set; }
        [UI("GeoJSON数据")] public string JsonData { get; set; }

        //
        public virtual Org Org { get; set; }
        public virtual User Creator { get; set; }

        //
        public string CreatorName => Creator?.Name;
        public string OrgName => Org?.Name;

        // ITree 接口
        public override GisGeometry Clone()
        {
            return base.Clone().Let(t => {
                t.Alias = this.Alias;
                t.OrgId = this.OrgId;
                t.CreatorId = this.CreatorId;
                t.JsonData = this.JsonData;
            });
        }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                Alias,
                SortId,
                OrgId,
                CreatorId,
                JsonData,
                Children,

                OrgName,
                CreatorName,
            };
        }


        public static IQueryable<GisGeometry> Search(string name, long? creator, long? orgId, long? parentId)
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
