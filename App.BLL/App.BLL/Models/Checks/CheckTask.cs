using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>
    /// 检查任务类型
    /// </summary>
    public enum CheckTaskType
    {
        [UI("日常")] Daily = 1,
        [UI("专项")] Special = 2,
    }

    /*
    检查任务
    CheckTask
              --1:n--CheckTaskObject（要检查的对象）
              --n:1--Org（承接组织，单组织负责制）
     */
    [UI("检查", "检查任务")]
    public class CheckTask : TreeEntity<CheckTask>
    {
        [UI("类型")]      public CheckTaskType? Type { get; set; } = CheckTaskType.Special;
        [UI("总数量")]     public long? TotalCount {get;set;}
        [UI("已完成数量")]  public long? FinishCount {get;set;}
        [UI("任务进度")]   public float? Progress { get; set; }
        [UI("备注")]      public string Remark { get; set; }
        [UI("开始时间")]   public DateTime? StartDt { get; set; }
        [UI("截止时间")]   public DateTime? ExpireDt { get; set; }
        [UI("是否活动中")] public bool IsActive => (StartDt <= DateTime.Now && ExpireDt > DateTime.Now);
        [UI("承接组织")]   public long? OrgId { get; set; }             // 承接组织（单组织负责制）


        // Relations
        public virtual User Creator { get; set; }                       // 发布人
        public virtual Org Org { get; set; }                           // 承接组织导航
        public virtual List<CheckTaskObject> CheckObjects { get; set; } // 要检查的对象
        public virtual List<CheckTaskHazard> CheckHazards { get; set; } // 要检查的风险（未使用）
        public virtual List<Check> Checks { get; set; } = new List<Check>(); // 关联检查记录（未使用）

        public virtual string CreatorName => Creator?.Name;
        public virtual string OrgName => Org?.Name;


        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                CreatorId,
                CreatorName,
                Remark,
                StartDt,
                ExpireDt,
                TotalCount,
                FinishCount,
                Progress,
                IsActive,
                OrgId,
                OrgName,
                CheckObjectIds = CheckObjects?.Select(o => o.ObjectId).ToList(),
            };
        }

        public static IQueryable<CheckTask> Search(string name=null, DateTime? startDt=null, DateTime? expireDt=null, bool? isActive=null, long? creatorId=null, long? orgId=null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())          q = q.Where(o => o.Name.Contains(name.Trim()));
            if (startDt.IsNotEmpty())       q = q.Where(o => o.StartDt >= startDt.Value);
            if (expireDt.IsNotEmpty())      q = q.Where(o => o.ExpireDt <= expireDt.Value);
            if (isActive.IsNotEmpty())      q = q.Where(o => o.IsActive == isActive.Value);
            if (creatorId.IsNotEmpty())     q = q.Where(o => o.CreatorId == creatorId.Value);
            if (orgId.IsNotEmpty())
            {
                // 承接组织及下属
                var orgIds = Org.GetChildIds(orgId.Value);
                // 任务本身承接组织属于指定 org（及下属）
                q = q.Where(t => (t.OrgId != null && orgIds.Contains(t.OrgId.Value))
                        || t.CheckObjects.Any(co => orgIds.Contains(co.OrgId.Value))
                        || t.CheckHazards.Any(ch => orgIds.Contains(ch.OrgId.Value)));
            }
            return q;
        }


        /// <summary>根据检查对象完成情况刷新任务的总数、完成数与进度百分比</summary>
        public void UpdateProgress()
        {
            var objs = CheckTaskObject.Search(taskId:this.Id).ToList();
            TotalCount = objs.Count;
            FinishCount = objs.Count(o => o.IsFinished);
            Progress = TotalCount > 0 ? (float)FinishCount / TotalCount * 100 : 0;
        }
    }
}
