using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>图层类型</summary>
    public enum LayerType
    {
        [UI("点")]  Point = 1,
        [UI("几何")] Geometry = 2,
        [UI("图片")] Image = 3,
    }


    /// <summary>GIS几何图形</summary>
    [UI("GIS", "GIS几何图形")]
    public class GisGeometry : EntityBase<GisGeometry>
    {
        [UI("名称")]        public string Name { get; set; }
        [UI("排序")]        public int SortId { get; set; }
        [UI("简称")]        public string Alias { get; set; }
        [UI("GIS菜单")]     public long? MenuId { get; set; }
        [UI("地址")]        public string Addr { get; set; }
        [UI("经纬度")]      public string GPS { get; set; }
        [UI("GeoJSON数据")] public string GeoJson { get; set; }
        [UI("扩展数据")]    public string DataJson { get; set; }

        //
        public virtual GisMenu Menu { get; set; }
        public virtual User Creator { get; set; }

        //
        public string MenuName => Menu?.Name;
        public string MenuIcon => Menu?.Icon;
        public string CreatorName => Creator?.Name;

        public virtual GisGeometry Clone()
        {
            return new GisGeometry().Let(t => {
                t.Id = this.Id;
                t.Name = this.Name;
                t.SortId = this.SortId;
                t.Alias = this.Alias;
                t.MenuId = this.MenuId;
                t.Addr = this.Addr;
                t.CreatorId = this.CreatorId;
                t.GeoJson = this.GeoJson;
                t.GPS = this.GPS;
                t.DataJson = this.DataJson;
            });
        }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                Alias,
                SortId,
                MenuId,
                Addr,
                CreatorId,
                GeoJson,
                GPS,
                DataJson,

                MenuName,
                MenuIcon,
                CreatorName,
            };
        }


        public static IQueryable<GisGeometry> Search(string name, long? creator, long? menuId)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())       q = q.Where(o => o.Name.Contains(name.Trim()));
            if (creator.IsNotEmpty())    q = q.Where(o => o.CreatorId == creator.Value);
            if (menuId.IsNotEmpty())     q = q.Where(o => o.MenuId == menuId.Value);
            return q;
        }
    }
}
