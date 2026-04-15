using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>区域类别</summary>
    public enum RegionType
    {
        [UI("椭圆")] Ellipse = 0,
        [UI("三角形")] Triangle = 1,
        [UI("矩形")] Rectangle = 2,
        [UI("多边形")] Polygon = 3
    }

    /// <summary>GIS区域管理</summary>
    [UI("GIS", "GIS区域")]
    public class GisRegion : TreeEntity<GisRegion>
    {
        [UI("区域类别")]     public RegionType RegionType { get; set; }
        [UI("简称")]        public string Alias { get; set; }
        [UI("责任组织")]     public long? OrgId { get; set; }
        [UI("Json数据")]     public string JsonData { get; set; }

        //
        public virtual Org Org { get; set; }
        public virtual User Creator { get; set; }

        //
        public string CreatorName => Creator?.Name;
        public string OrgName => Org?.Name;

        // ITree 接口
        public override GisRegion Clone()
        {
            return base.Clone().Let(t => {
                t.RegionType = this.RegionType;
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
                RegionType,
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


        public static IQueryable<GisRegion> Search(string name, RegionType? regionType, long? creator, long? orgId)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())       q = q.Where(o => o.Name.Contains(name.Trim()));
            if (regionType.IsNotEmpty()) q = q.Where(o => o.RegionType == regionType.Value);
            if (creator.IsNotEmpty())    q = q.Where(o => o.CreatorId == creator.Value);
            if (orgId.IsNotEmpty())      q = q.Where(o => o.OrgId == orgId.Value);
            return q;
        }
    }
}
