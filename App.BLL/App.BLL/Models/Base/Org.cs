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
        [UI("国家")]  Country = 0,
        [UI("省")]  Province = 1,
        [UI("地级市")]  City = 2,
        [UI("区县")]  District = 3,
        [UI("乡镇街道")]  Town = 4,
        [UI("社区")]  Community = 5,
        [UI("网格")]  Net = 6,
        [UI("单位", Color = "Red")]  Unit = 7,
        [UI("科室")]  Dept = 8,
    }

    /// <summary>组织</summary>
    public class Org : TreeEntity<Org>, IFixAll
    {
        [UI("备注")] public string Remark { get; set; }
        [UI("级别")] public OrgLevel? Level { get; set; }
        [UI("定位")] public string Gps { get; set; }
        [UI("图形数据")] public string GeoData { get; set; }

        
        // 上级网格名称（先别加，加上后导出超级慢）
        /* 
        [UI("归属城市")]   public string CityName      => this.GetAncestor(OrgLevel.City)?.Name;
        [UI("归属区县")]   public string DistrictName  => this.GetAncestor(OrgLevel.District)?.Name;
        [UI("归属乡镇")]   public string TownName      => this.GetAncestor(OrgLevel.Town)?.Name;
        [UI("归属社区")]   public string CommunityName => this.GetAncestor(OrgLevel.Community)?.Name;
        [UI("归属单位")]   public string UnitName      => this.GetAncestor(OrgLevel.Unit)?.Name;
        [UI("归属科室")]   public string DeptName      => this.GetAncestor(OrgLevel.Dept)?.Name;
        */
        
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
                Gps,
                GeoData = (mode == ExportMode.Detail) ? GeoData : null,
                Children = Children?.Select(t => t.Export(mode)).ToList(),  // 递归导出子组织（以减少子节点的字段输出）
            };
        }
        
        public override Org Clone()
        {
            return base.Clone()
                .SetValue(t => t.Remark, this.Remark)
                .SetValue(t => t.Level, this.Level)
                .SetValue(t => t.Gps, this.Gps)
                .SetValue(t => t.GeoData, this.GeoData)
                ;
        }

        /// <summary>查找指定级别的上级组织（包括自身）</summary>
        public Org GetAncestor(OrgLevel level)
        {
            return this.GetAncestor(Org.All, t => t.Level == level) as Org;
        }

        /// <summary>修正所有记录的“全称”信息</summary>
        public static int FixAll()
        {
            foreach (var item in Org.All)
            {
                item.FullName = item.GetFullName();
                item.Save();
            }
            Org.ClearCache();
            return Org.All.Count;
        }
    }
}
