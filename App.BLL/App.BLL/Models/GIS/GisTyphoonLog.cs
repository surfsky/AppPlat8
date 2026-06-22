using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS 台风轨迹</summary>
    [UI("GIS", "GIS台风轨迹")]
    public class GisTyphoonLog : EntityBase<GisTyphoonLog>
    {
        [UI("台风编号")] public string Code { get; set; }
        [UI("时间")] public DateTime? TimeUtc { get; set; }
        [UI("经度")] public double? Lng { get; set; }
        [UI("纬度")] public double? Lat { get; set; }
        [UI("气压")] public int? Pressure { get; set; }
        [UI("风速")] public int? WindMs { get; set; }
        [UI("等级码")] public int? LevelCode { get; set; }
        [UI("等级")] public string LevelName { get; set; }
        [UI("序号")] public int? SortId { get; set; }

        [NotMapped]
        [UI("年份")]
        public int? Year => TimeUtc?.Year ?? GisTyphoonImporter.GetYear(Code);

        [NotMapped]
        [UI("经纬度")]
        public string Gps => (Lng.HasValue && Lat.HasValue) ? $"{Lng.Value},{Lat.Value}" : "";

        /// <summary>导出对象</summary>
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Code,
                TimeUtc,
                Lng,
                Lat,
                Gps,
                Pressure,
                WindMs,
                LevelCode,
                LevelName,
                SortId,
                Year,
                CreateDt,
                UpdateDt,
            };
        }

        /// <summary>搜索轨迹</summary>
        public static IQueryable<GisTyphoonLog> Search(string code = null, int? year = null, DateTime? startUtc = null, DateTime? endUtc = null)
        {
            var q = ValidSet.AsQueryable();
            if (code.IsNotEmpty())
            {
                var key = code.Trim();
                q = q.Where(t => t.Code == key);
            }
            if (year.HasValue && year.Value > 1900)
            {
                var prefix = year.Value.ToString();
                q = q.Where(t => (t.Code ?? string.Empty).StartsWith(prefix));
            }
            if (startUtc.HasValue)
                q = q.Where(t => t.TimeUtc >= startUtc.Value);
            if (endUtc.HasValue)
                q = q.Where(t => t.TimeUtc <= endUtc.Value);
            return q.OrderBy(t => t.TimeUtc).ThenBy(t => t.SortId).ThenBy(t => t.Id);
        }
    }
}
