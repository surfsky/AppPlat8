using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

namespace App.DAL.OA
{
    /// <summary>会议类型</summary>
    public enum MeetingType
    {
        [UI("周会")] Week = 1,
        [UI("月会")] Month = 2,
        [UI("培训会")] Training = 3,
        [UI("其它")] Other = 9,
    }

    [UI("OA", "会议表")]
    public class Meeting : EntityBase<Meeting>
    {
        [UI("日期")]   public DateTime Day { get; set; }
        [UI("类型")]   public MeetingType? Type { get; set; }
        [UI("科室")]   public long? OrgId { get; set; }
        [UI("内容")]   public string Content { get; set; }
        [UI("图片")]   public string Image { get; set; }

        public virtual Org Org { get; set; }
        public string OrgName => Org?.Name;
        public string TypeName => Type.GetTitle();


        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Day,
                Type,
                OrgId,
                Content,
                Image,
                CreateDt,
                UpdateDt,
            };
        }

        /// <summary>按日期+会议类型+科室+内容关键字检索</summary>
        public static IQueryable<Meeting> Search(
            DateTime? fromDt = null,
            DateTime? toDt = null,
            long? orgId = null,
            MeetingType? type = null,
            string keyword = "")
        {
            var q = IncludeSet.AsQueryable();
            if (fromDt.IsNotEmpty())     q = q.Where(o => o.Day >= fromDt);
            if (toDt.IsNotEmpty())       q = q.Where(o => o.Day <= toDt);
            if (keyword.IsNotEmpty())    q = q.Where(o => o.Content.Contains(keyword));
            if (orgId.IsNotEmpty())      q = q.Where(o => o.OrgId == orgId);
            if (type.IsNotEmpty())       q = q.Where(o => o.Type == type);
            return q;
        }
    }
}
