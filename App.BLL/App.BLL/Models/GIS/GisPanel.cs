using System;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS统计面板配置</summary>
    [UI("GIS", "GIS统计面板")]
    public class GisPanel : EntityBase<GisPanel>
    {
        [UI("标题")] public string Title { get; set; }
        [UI("提示信息")] public string Info { get; set; }
        [UI("位置/排序")] public int Position { get; set; }
        [UI("普通内容")]
        public string Content { get; set; }
        [UI("图表配置JSON")]
        public string ChartJson { get; set; }
        [UI("GIS显示")]
        public bool InGis { get; set; }
        [UI("Dashboard显示")]
        public bool InDashboard { get; set; }

        public virtual User Creator { get; set; }
        public string CreatorName => Creator?.Name;

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Title,
                Info,
                Position,
                Content,
                ChartJson,
                InGis,
                InDashboard,
                CreatorId,
                CreateDt,
                UpdateDt,
                CreatorName,
            };
        }

        public static IQueryable<GisPanel> Search(string title, bool? inGis, bool? inDashboard)
        {
            var q = IncludeSet.AsQueryable();
            if (title.IsNotEmpty()) q = q.Where(t => t.Title.Contains(title.Trim()));
            if (inGis.HasValue) q = q.Where(t => t.InGis == inGis.Value);
            if (inDashboard.HasValue) q = q.Where(t => t.InDashboard == inDashboard.Value);
            return q;
        }
    }
}