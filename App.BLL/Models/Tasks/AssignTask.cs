using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text.Json.Serialization;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>
    /// 交办任务状态
    /// </summary>
    public enum AssignTaskStatus
    {
        [UI("发布")]        Published = 1,
        [UI("取消")]        Canceled = 2,
        [UI("完成")]        Completed = 3,
    }

    /// <summary>交办任务管理</summary>
    [UI("OA", "任务")]
    public class AssignTask : EntityBase<AssignTask>
    {
        [UI("父Id")]        public long? ParentId { get; set; }
        [UI("名称")]        public string Name { get; set; }
        [UI("发起人")]        public string Initiator { get; set; }
        [UI("责任人")]        public string PersonInCharge { get; set; }
        [UI("责任组织")]       public long? OrgId { get; set; }
        [UI("提交周期")]       public string Cycle { get; set; }
        [UI("开始日期")]       public DateTime? StartDt { get; set; }
        [UI("最后填报日期")]    public DateTime? LastReportDt { get; set; }
        [UI("下次填报日期")]    public DateTime? NextReportDt { get; set; }
        [UI("进度")]           public float? Progress { get; set; }
        [UI("状态")]           public AssignTaskStatus? Status { get; set; } // 发布、取消、完成

        [JsonIgnore]
        public virtual AssignTask Parent { get; set; }
        public virtual Org Org { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                Initiator,
                PersonInCharge,
                OrgId,
                Cycle,
                StartDt,
                LastReportDt,
                NextReportDt,
                Progress,
                Status,
                StatusName = Status?.GetTitle(),
                CreateDt
            };
        }

        public static IQueryable<AssignTask> Search(string name, long? orgId)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty()) q = q.Where(o => o.Name.Contains(name));
            if (orgId.IsNotEmpty()) q = q.Where(o => o.OrgId == orgId.Value);
            return q;
        }
    }

    /// <summary>任务处理历史</summary>
    [UI("OA", "任务处理历史")]
    public class AssignTaskLog : EntityBase<AssignTaskLog>
    {
        [UI("任务Id")]       public long? TaskId { get; set; }
        [UI("处理人")]       public string Handler { get; set; }
        [UI("处理进度")]     public int? Progress { get; set; }
        [UI("状态")]        public AssignTaskStatus? Status { get; set; }
        public virtual AssignTask Task { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TaskId,
                TaskName = Task?.Name,
                Handler,
                Progress,
                Status,
                CreateDt
            };
        }

        public static IQueryable<AssignTaskLog> Search(long? taskId)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty()) q = q.Where(o => o.TaskId == taskId.Value);
            return q;
        }
    }
}
