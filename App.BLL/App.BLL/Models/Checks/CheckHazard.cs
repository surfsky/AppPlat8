using System;
using System.Collections.Generic;
using System.Linq;
using App.Entities;
using App.Utils;

/**
检查记录表CheckLog --(1:n)-- CheckHazard 隐患表 --(1:n)--CheckHazardReview 隐患复查记录表
*/
namespace App.DAL
{
    //-------------------------------------------------------
    // 其他枚举值
    //-------------------------------------------------------
    [UI("检查", "隐患状态")]
    public enum CheckHazardStatus
    {
        [UI("待整改")] Pending = 0,
        [UI("整改中")] Rectifying = 1,
        [UI("已整改")] Rectified = 2,
        [UI("已关闭")] Closed = 3,
    }

    //-------------------------------------------------------
    /// 隐患
    //-------------------------------------------------------
    [UI("检查", "隐患")]
    public class CheckHazard : EntityBase<CheckHazard>
    {
        [UI("检查对象")] public long? ObjectId { get; set; }    // 冗余存储，方便查询
        [UI("检查人员")] public long? CheckerId { get; set; }   // 冗余存储，方便查询
        [UI("检查记录")] public long? CheckLogId { get; set; }
        [UI("检查表Id")] public long? CheckSheetId { get; set; }
        [UI("检查项Id")] public long? CheckItemId { get; set; }
        [UI("检查项内容")] public string CheckItemText { get; set; } // 冗余存储，防止检查项删除
        [UI("隐患描述")] public string Description { get; set; }
        [UI("整改状态")] public CheckHazardStatus? Status { get; set; } = CheckHazardStatus.Pending;
        [UI("整改期限")] public DateTime? ExpireDt { get; set; }
        [UI("整改日期")] public DateTime? RectifyDt { get; set; }
        [UI("录入141")] public bool? IsIn141 { get; set; }

        //
        public virtual CheckObject CheckObject { get; set; }
        public virtual User Checker { get; set; }
        public virtual Check Check { get; set; }
        public virtual CheckSheet CheckSheet { get; set; }
        public virtual CheckSheetItem CheckItem { get; set; }
        public virtual List<CheckHazardLog> Reviews { get; set; }

        public override object Export(ExportMode mode)
        {
            return new
            {
                Id,
                ObjectId,
                ObjectName = CheckObject?.Name,
                CheckerId,
                CheckerName = Checker?.Name,
                CheckLogId,
                CheckItemId,
                CheckItemText,
                Description,
                Images,
                Status,
                StatusName = Status.GetTitle(),
                CreateDt,
                ExpireDt,
                RectifyDt,
                IsIn141
            };
        }
        public static IQueryable<CheckHazard> Search(string objectName, long? objectId, string checkerName, long? checkerId, CheckHazardStatus? status, DateTime? createStartDt)
        {
            IQueryable<CheckHazard> q = CheckHazard.IncludeSet;
            if (objectId.IsNotEmpty())     q = q.Where(o => o.ObjectId == objectId.Value);
            if (checkerId.IsNotEmpty())    q = q.Where(o => o.Check.CheckerId == checkerId.Value);
            if (objectName.IsNotEmpty())   q = q.Where(o => o.CheckObject.Name.Contains(objectName.Trim()));
            if (checkerName.IsNotEmpty())  q = q.Where(o => o.Checker.Name.Contains(checkerName.Trim()));
            if (status.IsNotEmpty())       q = q.Where(o => o.Status == status.Value);
            if (createStartDt.IsNotEmpty()) q = q.Where(o => o.CreateDt >= createStartDt.Value.Date);
            return q;
        }
    }

    //-------------------------------------------------------
    // 隐患复查记录
    //-------------------------------------------------------
    [UI("检查", "复查记录")]
    public class CheckHazardLog : EntityBase<CheckHazardLog>
    {
        [UI("隐患")] public long HazardId { get; set; }
        [UI("记录人")] public long? ReviewerId { get; set; }
        [UI("复查时间")] public DateTime? ReviewDt { get; set; }
        [UI("说明")] public string Remark { get; set; }
        [UI("整改状态")] public string Status { get; set; }

        public virtual CheckHazard Hazard { get; set; }
        public virtual User Reviewer { get; set; }

        public override object Export(ExportMode mode)
        {
            return new
            {
                Id,
                HazardId,
                ReviewerId,
                ReviewDt,
                Remark,
                Images,
                Status
            };
        }

        public static IQueryable<CheckHazardLog> Search(long? hazardId, long? reviewerId, DateTime? reviewDt)
        {
            IQueryable<CheckHazardLog> q = CheckHazardLog.IncludeSet;
            if (hazardId.IsNotEmpty())   q = q.Where(o => o.HazardId == hazardId.Value);
            if (reviewerId.IsNotEmpty()) q = q.Where(o => o.ReviewerId == reviewerId.Value);
            if (reviewDt.IsNotEmpty())   q = q.Where(o => o.ReviewDt == reviewDt.Value.Date);
            return q;
        }
    }    
}
