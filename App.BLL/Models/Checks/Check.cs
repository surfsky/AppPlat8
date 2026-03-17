using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

/**
检查记录表CheckLog --(1:n)-- CheckHazard 隐患表 --(1:n)--CheckHazardReview 隐患复查记录表
*/
namespace App.DAL
{
    /// <summary>检查记录</summary>
    [UI("检查", "检查记录")]
    public class Check : EntityBase<Check>
    {
        [UI("任务")]    public long? TaskId { get; set; }
        [UI("检查科室")] public long? OrgId { get; set; }
        [UI("检查人员")] public long? CheckerId { get; set; }
        [UI("检查对象")] public long? CheckObjectId { get; set; }
        [UI("检查表")]   public long? CheckSheetId { get; set; }
        [UI("检查项Id")] public long? CheckItemId { get; set; }
        [UI("检查结果")] public bool? Result { get; set; } = true;
        [UI("是否关闭")] public bool? IsClosed { get; set; } = false;  // 关闭后不允许修改
        [UI("检查时间")] public DateTime? CheckDt { get; set; }
        [UI("隐患数")]    public int? HazardCount { get; set; } = 0;
        [UI("剩余隐患数")] public int? RemainHazardCount { get; set; } = 0;

        // Relations
        public virtual CheckObject CheckObject { get; set; }
        public virtual CheckTask Task { get; set; }
        public virtual CheckSheet CheckSheet { get; set; }
        public virtual CheckSheetItem CheckItem { get; set; }
        public virtual Org Org { get; set; }
        public virtual User Checker { get; set; }
        [NotMapped]
        public virtual List<CheckHazard> Hazards { get; set; }


        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                CheckDt,
                CheckObjectId,
                CheckObjectName = CheckObject?.Name,
                TaskId,
                TaskName = Task?.Name,
                OrgId,
                OrgName = Org?.Name,
                CheckerId,
                CheckerName = Checker?.Name,
                CheckSheetId,
                CheckSheetName = CheckSheet?.Name,
                CheckItemId,
                CheckItemName = CheckItem?.Name,
                Result,
                IsClosed,
                HazardCount,
                RemainHazardCount
            };
        }

        public static IQueryable<Check> Search(string objectName, string socialCreditCode, long? objectId, CheckObjectType? objectType, DateTime? checkStartDt, DateTime? checkEndDt)
        {
            IQueryable<Check>                q = Check.IncludeSet;
            if (objectName.IsNotEmpty())        q = q.Where(o => o.CheckObject.Name.Contains(objectName.Trim()));
            if (objectId.IsNotEmpty())          q = q.Where(o => o.CheckObjectId == objectId.Value);
            if (objectType.IsNotEmpty())        q = q.Where(o => o.CheckObject.ObjectType == objectType.Value);
            if (socialCreditCode.IsNotEmpty())  q = q.Where(o => o.CheckObject.SocialCreditCode.Contains(socialCreditCode.Trim()));
            if (checkStartDt.IsNotEmpty())      q = q.Where(o => o.CheckDt >= checkStartDt.Value);
            if (checkEndDt.IsNotEmpty())        q = q.Where(o => o.CheckDt <= checkEndDt.Value);
            return q;
        }
    }


}
