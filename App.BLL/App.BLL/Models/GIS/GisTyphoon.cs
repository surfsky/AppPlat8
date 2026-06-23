using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS 台风</summary>
    [UI("GIS", "GIS台风")]
    public class GisTyphoon : EntityBase<GisTyphoon>
    {
        [UI("编号")] public string Code { get; set; }
        [UI("英文名")] public string Name { get; set; }
        [UI("中文名")] public string ChineseName { get; set; }
        [UI("生成时间")] public DateTime? BirthUtc { get; set; }
        [UI("结束时间")] public DateTime? DeathUtc { get; set; }
        [UI("最高等级")] public int? MaxLevel { get; set; }
        [UI("是否登陆")] public bool? IsLand { get; set; } = false;

        [NotMapped]
        [UI("年份")]
        public int? Year => GisTyphoonImporter.GetYear(Code);

        [NotMapped]
        [UI("显示名")]
        public string DisplayName => $"{Code} {(ChineseName.IsNotEmpty() ? ChineseName : Name)}".Trim();

        /// <summary>导出对象</summary>
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Code,
                Name,
                ChineseName,
                BirthUtc,
                DeathUtc,
                MaxLevel,
                IsLand,
                Year,
                DisplayName,
                CreateDt,
                UpdateDt,
            };
        }

        /// <summary>搜索台风</summary>
        public static IQueryable<GisTyphoon> Search(string name = null, string code = null, int? year = null)
        {
            var q = ValidSet.AsQueryable();
            if (name.IsNotEmpty())
            {
                var key = name.Trim();
                q = q.Where(t =>
                    (t.Name ?? string.Empty).Contains(key) ||
                    (t.ChineseName ?? string.Empty).Contains(key));
            }
            if (code.IsNotEmpty())
            {
                var key = code.Trim();
                q = q.Where(t => (t.Code ?? string.Empty).Contains(key));
            }
            if (year.HasValue && year.Value > 1900)
            {
                var prefix = year.Value.ToString();
                q = q.Where(t => (t.Code ?? string.Empty).StartsWith(prefix));
            }
            return q;
        }
    }
}
