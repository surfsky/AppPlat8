using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS数据来源</summary>
    public enum GisDataFrom
    {
        [UI("Geometry")] Geometry = 1,   // 来自 Geometry点位表
        [UI("API")]      API = 2,        // 来自 API 表
    }

    /// <summary>GIS菜单</summary>
    [UI("GIS", "GIS菜单")]
    public class GisMenu : TreeEntity<GisMenu>, IFix<GisMenu>, IFixAll
    {
        [UI("责任组织")]      public long? OrgId { get; set; }
        [UI("图标")]         public string Icon { get; set; }
        [UI("默认显示")]      public bool IsDefaultShow { get; set; }
        [UI("数据来源")]      public GisDataFrom? DataFrom { get; set; } = GisDataFrom.Geometry;
        [UI("点位数")]        public int? DataCnt { get; set; }
        [UI("最后数据时间")]   public DateTime? DataDt { get; set; }

        //
        public virtual Org Org { get; set; }
        public virtual User Creator { get; set; }
        public string OrgName => Org?.Name;
        public string CreatorName => Creator?.Name;

        // ITree 接口
        public override GisMenu Clone()
        {
            return base.Clone().Let(t => {
                t.OrgId = this.OrgId;
                t.Icon = this.Icon;
                t.CreatorId = this.CreatorId;
                t.IsDefaultShow = this.IsDefaultShow;
                t.DataCnt = this.DataCnt;
                t.DataDt = this.DataDt;
                t.DataFrom = this.DataFrom;
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
                Icon,
                CreatorId,
                IsDefaultShow,
                Children,
                DataCnt,
                DataDt,
                DataFrom,

                OrgName,
                CreatorName,
            };
        }


        public static IQueryable<GisMenu> Search(string name=null, long? creatorId=null, long? orgId=null, long? parentId=null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())         q = q.Where(o => o.Name.Contains(name.Trim()));
            if (creatorId.IsNotEmpty())    q = q.Where(o => o.CreatorId == creatorId.Value);
            if (orgId.IsNotEmpty())        q = q.Where(o => o.OrgId == orgId.Value);
            if (parentId.IsNotEmpty())     q = q.Where(o => o.ParentId == parentId.Value);
            return q;
        }

        /// <summary>修复数据</summary>
        public GisMenu Fix()
        {
            // Geometry 变化会影响父节点汇总值，直接执行全量汇总更稳妥。
            FixAll();
            return this;
        }

        /// <summary>修复所有数据（递归更新点位数和最后数据时间）</summary>
        public static int FixAll()
        {
            var menus = Set.ToList();
            if (menus.Count == 0)
                return 0;

            var dataCntMap = GisGeometry.Set
                .Where(t => t.MenuId != null)
                .GroupBy(t => t.MenuId.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            var apiCntMap = GisApi.Set
                .Where(t => t.MenuId != null)
                .GroupBy(t => t.MenuId.Value)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.DataCnt ?? 0));

            var menuMap = menus.ToDictionary(t => t.Id);
            var childLookup = menus.ToLookup(t => t.ParentId);
            var now = DateTime.Now;

            int SumCnt(long menuId)
            {
                var geoCnt = dataCntMap.TryGetValue(menuId, out var directGeoCnt) ? directGeoCnt : 0;
                var apiCnt = apiCntMap.TryGetValue(menuId, out var directApiCnt) ? directApiCnt : 0;
                var own = geoCnt + apiCnt;
                var childCnt = childLookup[menuId].Sum(child => SumCnt(child.Id));
                var total = own + childCnt;

                var menu = menuMap[menuId];
                menu.DataCnt = total;
                menu.DataDt = now;
                return total;
            }

            var roots = menus
                .Where(t => !t.ParentId.HasValue || !menuMap.ContainsKey(t.ParentId.Value))
                .ToList();

            foreach (var root in roots)
                SumCnt(root.Id);

            Db.SaveChanges();
            ClearCache();

            return menus.Count;
        }
    }
}
