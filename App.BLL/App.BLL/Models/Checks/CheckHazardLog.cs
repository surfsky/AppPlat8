using System;
using System.Linq;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

/**
检查记录表CheckLog --(1:n)-- CheckHazard 隐患表 --(1:n)--CheckHazardReview 隐患复查记录表
*/
namespace App.DAL
{
    //-------------------------------------------------------
    // 隐患处理记录
    //-------------------------------------------------------
    [UI("检查", "处理记录")]
    public class CheckHazardLog : EntityBase<CheckHazardLog>
    {
        [UI("隐患")] public long HazardId { get; set; }
        [UI("记录人")] public long? ReviewerId { get; set; }
        [UI("处理时间")] public DateTime? ReviewDt { get; set; }
        [UI("处理内容")] public string Content { get; set; }
        [UI("整改状态")] public CheckHazardStatus? Status { get; set; }
        [UI("处理图片")] public string Image { get; set; }

        public virtual CheckHazard Hazard { get; set; }
        public virtual User Reviewer { get; set; }

        public string ReviewerName => Reviewer?.Name;
        public string StatusName => Status.GetTitle();

        public override object Export(ExportMode mode)
        {
            return new
            {
                Id,
                HazardId,
                ReviewerId,
                ReviewDt,
                ReviewDtText = ReviewDt?.ToString("yyyy-MM-dd HH:mm"),
                Content,
                Status,
                Image,

                ReviewerName,
                StatusName,
            };
        }

        public static IQueryable<CheckHazardLog> Search(long? hazardId, long? reviewerId, DateTime? reviewDt)
        {
            IQueryable<CheckHazardLog> q = CheckHazardLog.IncludeSet.Include(t => t.Reviewer);
            if (hazardId.IsNotEmpty())   q = q.Where(o => o.HazardId == hazardId.Value);
            if (reviewerId.IsNotEmpty()) q = q.Where(o => o.ReviewerId == reviewerId.Value);
            if (reviewDt.IsNotEmpty())   q = q.Where(o => o.ReviewDt == reviewDt.Value.Date);
            return q;
        }
    }    
}