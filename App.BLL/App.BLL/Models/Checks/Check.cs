using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

/**
检查记录表CheckLog --(1:n)-- CheckHazard 隐患表 --(1:n)--CheckHazardReview 隐患复查记录表
*/
namespace App.DAL
{
    /// <summary>检查记录</summary>
    [UI("检查", "检查记录")]
    public class Check : EntityBase<Check>
    {
        [UI("ID"), SnowflakeId]     public override long Id { get; set; }
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
        [UI("任务ID列表"), NotMapped] public List<long> TaskIds { get; set; } = new List<long>();
        [UI("任务列表"), NotMapped] public string TaskNames { get; set; } = string.Empty;
        [UI("关联任务")] public virtual List<CheckTask> Tasks { get; set; } = new List<CheckTask>();
        public virtual CheckObject CheckObject { get; set; }
        public virtual CheckSheet CheckSheet { get; set; }
        public virtual CheckSheetItem CheckItem { get; set; }
        public virtual Org Org { get; set; }
        public virtual User Checker { get; set; }
        //[NotMapped]
        public virtual List<CheckHazard> Hazards { get; set; }
        public string OrgName => Org?.Name ?? string.Empty;
        public string CheckerName => Checker?.Name ?? string.Empty;
        public string CheckObjectName => CheckObject?.Name ?? string.Empty;
        public string CheckSheetName => CheckSheet?.Name ?? string.Empty;
        public string CheckItemName => CheckItem?.Name ?? string.Empty;

        //-----------------------------------------------------------------
        //
        //-----------------------------------------------------------------
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            var taskIds = Tasks?.Select(t => t.Id).Distinct().ToList() ?? new List<long>();
            var taskNames = Tasks?
                .Where(t => t != null && t.Name.IsNotEmpty())
                .Select(t => t.Name.Trim())
                .Distinct()
                .ToJoinString(",") ?? string.Empty;
            return new
            {
                Id,
                CheckDt,
                CheckObjectId,
                TaskIds = taskIds,
                TaskNames = taskNames,
                OrgId,
                CheckerId,
                CheckSheetId,
                CheckItemId,
                Result,
                IsClosed,
                HazardCount,
                RemainHazardCount,
                CreateDt,
                CreatorId,

                OrgName,
                CheckerName,
                CheckObjectName,
                CheckSheetName,
                CheckItemName,
            };
        }

        /// <summary>获取检查详情（包含任务等关联数据）。</summary>
        public new static Check GetDetail(long id)
        {
            return BuildIncludeQuery().FirstOrDefault(t => t.Id == id).Let(o =>
            {
                if (o != null)
                    SyncTaskFields(o);
            });
        }

        /// <summary>按条件获取检查详情（包含任务等关联数据）。</summary>
        public new static Check GetDetail(System.Linq.Expressions.Expression<Func<Check, bool>> condition)
        {
            return BuildIncludeQuery().Where(condition).FirstOrDefault().Let(o =>
            {
                if (o != null)
                    SyncTaskFields(o);
            });
        }

        /// <summary>构造包含关联数据的查询。</summary>
        static IQueryable<Check> BuildIncludeQuery()
        {
            return DataSet
                .Include(o => o.Tasks)
                .Include(o => o.CheckObject)
                .Include(o => o.CheckSheet)
                .Include(o => o.CheckItem)
                .Include(o => o.Org)
                .Include(o => o.Checker)
                .AsSplitQuery();
        }

        /// <summary>同步任务回填字段。</summary>
        static void SyncTaskFields(Check item)
        {
            item.TaskIds = item.Tasks?.Select(t => t.Id).Distinct().ToList() ?? new List<long>();
            item.TaskNames = item.Tasks?
                .Where(t => t != null && t.Name.IsNotEmpty())
                .Select(t => t.Name.Trim())
                .Distinct()
                .ToJoinString(",") ?? string.Empty;
        }

        /// <summary>设置关联任务列表。</summary>
        public void SetTasks(List<long> taskIds, bool save = false)
        {
            taskIds = (taskIds ?? new List<long>()).Where(t => t > 0).Distinct().ToList();
            var tasks = taskIds.Count == 0
                ? new List<CheckTask>()
                : CheckTask.Set.Where(t => taskIds.Contains(t.Id)).ToList();
            Tasks.Clear();
            foreach (var task in tasks)
                Tasks.Add(task);
            SyncTaskFields(this);
            if (save)
                this.Save();
        }

        /// <summary>搜索检查记录。</summary>
        public static IQueryable<Check> Search(string objectName, string socialCreditCode, long? objectId, CheckObjectType? objectType, DateTime? checkStartDt, DateTime? checkEndDt)
        {
            IQueryable<Check> q = BuildIncludeQuery();
            if (objectName.IsNotEmpty())        q = q.Where(o => o.CheckObject.Name.Contains(objectName.Trim()));
            if (objectId.IsNotEmpty())          q = q.Where(o => o.CheckObjectId == objectId.Value);
            if (objectType.IsNotEmpty())        q = q.Where(o => o.CheckObject.ObjectType == objectType.Value);
            if (socialCreditCode.IsNotEmpty())  q = q.Where(o => o.CheckObject.SocialCreditId.Contains(socialCreditCode.Trim()));
            if (checkStartDt.IsNotEmpty())      q = q.Where(o => o.CheckDt >= checkStartDt.Value);
            if (checkEndDt.IsNotEmpty())        q = q.Where(o => o.CheckDt <= checkEndDt.Value);
            return q;
        }
    }
}
