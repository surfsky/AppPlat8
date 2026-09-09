using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.OA
{
    /// <summary>值班表</summary>
    [UI("OA", "值班表")]
    [Index(nameof(Day), IsUnique = true)]
    public class DutySchedule : EntityBase<DutySchedule>
    {
        [UI("基础", "日期")]      public DateTime Day { get; set; }
        [UI("人员", "值班领导")]   public string Leader { get; set; }    // 格式：张三/18900000000
        [UI("人员", "值班长")]     public string Chief { get; set; }     // 格式：张三/18900000000
        [UI("人员", "值班员1")]    public string Member1 { get; set; }
        [UI("人员", "值班员2")]    public string Member2 { get; set; }
        [UI("人员", "值班员3")]    public string Member3 { get; set; }
        [UI("人员", "值班员4")]    public string Member4 { get; set; }
        [UI("人员", "值班员5")]    public string Member5 { get; set; }
        [UI("人员", "值班员6")]    public string Member6 { get; set; }
        [UI("人员", "值班员7")]    public string Member7 { get; set; }
        [UI("人员", "值班员8")]    public string Member8 { get; set; }
        [UI("人员", "值班员9")]    public string Member9 { get; set; }
        [UI("人员", "值班员10")]   public string Member10 { get; set; }
        [UI("人员", "值班员11")]   public string Member11 { get; set; }
        [UI("人员", "值班员12")]   public string Member12 { get; set; }
        [UI("人员", "值班员13")]   public string Member13 { get; set; }
        [UI("人员", "值班员14")]   public string Member14 { get; set; }
        [UI("人员", "值班员15")]   public string Member15 { get; set; }

        [NotMapped]
        [UI("人员", "所有值班人员（聚合，用于过滤）")]
        public string[] AllMembers => new[] { Leader, Chief, Member1, Member2, Member3, Member4, Member5, Member6, Member7, Member8, Member9, Member10, Member11, Member12, Member13, Member14, Member15 };

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Day,
                Leader,
                Chief,
                Member1, Member2, Member3, Member4, Member5, Member6, Member7, Member8, Member9, Member10, Member11, Member12, Member13, Member14, Member15,
                CreateDt,
                UpdateDt,
            };
        }

        /// <summary>按日期+值班人员关键字检索</summary>
        public static IQueryable<DutySchedule> Search(
            DateTime? fromDt = null,
            DateTime? toDt = null,
            DateTime? day = null,
            string keyword = "")
        {
            var q = IncludeSet.AsQueryable();
            if (day.IsNotEmpty())
                q = q.Where(o => o.Day == day.Value.Date);
            else
            {
                if (fromDt.IsNotEmpty())
                    q = q.Where(o => o.Day >= fromDt.Value.Date);
                if (toDt.IsNotEmpty())
                    q = q.Where(o => o.Day <= toDt.Value.Date);
            }
            if (keyword.IsNotEmpty())
            {
                var k = keyword.Trim();
                q = q.Where(o =>
                    o.Leader.Contains(k) || o.Chief.Contains(k) ||
                    o.Member1.Contains(k) || o.Member2.Contains(k) || o.Member3.Contains(k) ||
                    o.Member4.Contains(k) || o.Member5.Contains(k) || o.Member6.Contains(k) ||
                    o.Member7.Contains(k) || o.Member8.Contains(k) || o.Member9.Contains(k) ||
                    o.Member10.Contains(k) || o.Member11.Contains(k) || o.Member12.Contains(k) ||
                    o.Member13.Contains(k) || o.Member14.Contains(k) || o.Member15.Contains(k));
            }
            return q;
        }
    }
}
