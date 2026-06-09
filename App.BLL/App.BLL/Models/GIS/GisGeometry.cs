using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

using Microsoft.EntityFrameworkCore;

namespace App.DAL.GIS
{
    /// <summary>图层类型</summary>
    public enum GeometryType
    {
        [UI("点")]  Point = 1,
        [UI("形状")] Shape = 2,
        [UI("文字")] Text = 3,
        [UI("图片")] Image = 4,
        [UI("监控")] Video = 5,
        [UI("文件")] File = 6,
    }


    /// <summary>GIS点位（包括点、几何、文字、图片、视频、文件等）</summary>
    [UI("GIS", "GIS点位")]
    public class GisGeometry : EntityBase<GisGeometry>
    {
        [UI("类型")]        public GeometryType? Type { get; set; } = GeometryType.Point;
        [UI("名称")]        public string Name { get; set; }
        [UI("别称")]        public string Alias { get; set; }
        [UI("GIS菜单")]     public long? MenuId { get; set; }
        [UI("排序")]        public int SortId { get; set; }
        [UI("地址")]        public string Addr { get; set; }
        [UI("链接地址")]     public string Url { get; set; }         // 通用链接地址（原 PageUrl）
        [UI("文件")]        public string File { get; set; }        // 文件链接（图片、模型、视频等）

        [UI("经纬度")]      public string Gps { get; set; }
        [UI("区域矩形")]    public string Region { get; set; }      // 图片显示区域：tlx,tly,brx,bry
        [UI("图形数据")]    public string GeoJson { get; set; }
        [UI("扩展数据")]    public string DataJson { get; set; }
        [UI("备注")]        public string Remark { get; set; }

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
                t.Type = this.Type;
                t.Name = this.Name;
                t.SortId = this.SortId;
                t.Alias = this.Alias;
                t.MenuId = this.MenuId;
                t.Addr = this.Addr;
                t.CreatorId = this.CreatorId;
                t.GeoJson = this.GeoJson;
                t.Gps = this.Gps;
                t.Region = this.Region;
                t.Url = this.Url;
                t.File = this.File;
                t.DataJson = this.DataJson;
            });
        }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Type,
                Name,
                Alias,
                SortId,
                MenuId,
                Addr,
                CreatorId,
                GeoJson,
                Gps,
                Region,
                Url,
                File,
                DataJson,

                MenuName,
                MenuIcon,
                CreatorName,
            };
        }


        /// <summary>搜索点位</summary>
        /// <param name="name"></param>
        /// <param name="creatorId"></param>
        /// <param name="type"></param>
        /// <param name="isValid"></param>
        /// <param name="menuId">菜单ID</param>
        /// <param name="recursive">是否递归检索子菜单</param>
        public static IQueryable<GisGeometry> Search(
            string name=null, 
            long? creatorId=null, 
            GeometryType? type = null, 
            bool? isValid = null,
            long? menuId=null, 
            bool recursive = false
        )
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())         q = q.Where(o => o.Name.Contains(name.Trim()));
            if (creatorId.IsNotEmpty())    q = q.Where(o => o.CreatorId == creatorId.Value);
            if (type.IsNotEmpty())         q = q.Where(o => o.Type == type.Value);
            if (menuId.IsNotEmpty())       
            {
                if (!recursive)
                    q = q.Where(o => o.MenuId == menuId.Value);
                else
                {
                    var menuIds = GisMenu.All.GetDescendants(menuId.Value).Select(m => m.Id).Distinct().ToList();
                    q = q.Where(g => g.MenuId.HasValue && menuIds.Contains(g.MenuId.Value));
                }
            }
            // 是否有效点位
            if (isValid.IsNotEmpty())
            {
                if (isValid.Value)
                    q = q.Where(o => !string.IsNullOrWhiteSpace(o.GeoJson) || !string.IsNullOrWhiteSpace(o.Gps));  // 有效点位，有GeoJson或Gps数据
                else
                    q = q.Where(o => string.IsNullOrWhiteSpace(o.GeoJson) && string.IsNullOrWhiteSpace(o.Gps));    // 无效点位，GeoJson或Gps数据为空
            }
            return q;
        }

        /// <summary>保存结束后，更新菜单统计数据</summary>
        public override void AfterChange(EntityOp op)
        {
            base.AfterChange(op);
            var menu = GetMenu();
            if (menu != null)
                menu.Fix();
        }

        GisMenu GetMenu()
        {
            if (this.Menu != null)
                return this.Menu;
            if (this.MenuId.HasValue)
                return GisMenu.Get(this.MenuId.Value);
            return null;
        }
    }
}
