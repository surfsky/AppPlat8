using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
    public class TyphoonLogsModel : AdminModel
    {
        public TyphoonLogGridItem Item { get; set; }

        /// <summary>初始化</summary>
        public void OnGet(string code = null)
        {
            Item = new TyphoonLogGridItem
            {
                Code = code
            };
        }

        /// <summary>获取轨迹</summary>
        public IActionResult OnGetData(Paging pi, string code)
        {
            if (pi.SortField.IsEmpty())
            {
                pi.SortField = "TimeUtc";
                pi.SortDirection = "ASC";
            }

            var list = GisTyphoonLog.Search(code: code)
                .Select(t => new TyphoonLogGridItem
                {
                    Id = t.Id,
                    Code = t.Code,
                    TimeUtc = t.TimeUtc,
                    Lng = t.Lng,
                    Lat = t.Lat,
                    Pressure = t.Pressure,
                    WindMs = t.WindMs,
                    LevelCode = t.LevelCode,
                    LevelName = t.LevelName,
                    SortId = t.SortId
                })
                .AsQueryable()
                .SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }
    }

    /// <summary>台风轨迹表格行</summary>
    public class TyphoonLogGridItem : IExport
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public DateTime? TimeUtc { get; set; }
        public double? Lng { get; set; }
        public double? Lat { get; set; }
        public int? Pressure { get; set; }
        public int? WindMs { get; set; }
        public int? LevelCode { get; set; }
        public string LevelName { get; set; }
        public int? SortId { get; set; }

        /// <summary>导出对象</summary>
        public object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Code,
                TimeUtc,
                Lng,
                Lat,
                Pressure,
                WindMs,
                LevelCode,
                LevelName,
                SortId
            };
        }
    }
}
