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
    CheckTask --1:n--CheckTaskOrg（受理组织）
              --1:n--CheckTaskObject（要检查的对象）
              --1:n--CheckTaskSheet（检查单）
    */
    [UI("检查", "检查任务")]
    public class CheckTask : TreeEntity<CheckTask>
    {
        [UI("类型")]      public CheckTaskType? Type { get; set; } = CheckTaskType.Daily;
        [UI("总数量")]     public long? TotalCount {get;set;}
        [UI("已完成数量")]  public long? FinishCount {get;set;}
        [UI("任务进度")]   public float? Progress { get; set; }
        [UI("备注")]      public string Remark { get; set; }
        [UI("开始时间")]   public DateTime? StartDt { get; set; }
        [UI("截止时间")]   public DateTime? ExpireDt { get; set; }
        [UI("是否活动中")] public bool IsActive => (StartDt <= DateTime.Now && ExpireDt > DateTime.Now);


        // Relations
        public virtual User Creator { get; set; }                       // 发布人
        public virtual List<CheckTaskOrg> Orgs { get; set; }            // 承接组织
        public virtual List<CheckTaskObject> CheckObjects { get; set; } // 要检查的对象
        public virtual List<CheckTaskSheet> CheckSheets { get; set; }   // 使用的检查表

        public virtual string CreatorName => Creator?.Name;


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
                OrgIds = Orgs?.Select(o => o.OrgId).ToList(),
                CheckObjectIds = CheckObjects?.Select(o => o.ObjectId).ToList(),
                CheckSheetIds = CheckSheets?.Select(o => o.SheetId).ToList()
            };
        }

        public static IQueryable<CheckTask> Search(string name, DateTime? startDt, DateTime? expireDt)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())          q = q.Where(o => o.Name.Contains(name.Trim()));
            if (startDt.IsNotEmpty())       q = q.Where(o => o.StartDt >= startDt.Value);
            if (expireDt.IsNotEmpty())      q = q.Where(o => o.ExpireDt <= expireDt.Value);
            return q;
        }

        public List<Org> GetOrgs()
        {
            return CheckTaskOrg.Search(this.Id, null).Select(o => o.Org).ToList();
        }
        public List<CheckObject> GetCheckObjects()
        {
            return CheckTaskObject.Search(this.Id).Select(o => o.Object).ToList();
        }
        public List<CheckSheet> GetCheckSheets()
        {
            return CheckTaskSheet.Search(this.Id, null).Select(o => o.Sheet).ToList();
        }
    }


    //==================================================================
    /// <summary>检查任务-组织关联</summary>
    public class CheckTaskOrg: EntityBase<CheckTaskOrg>
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("组织")] public long? OrgId { get; set; }

        // Relations
        public virtual CheckTask Task { get; set; }
        public virtual Org Org { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TaskId,
                TaskName = Task?.Name,
                OrgId,
                OrgName = Org?.Name
            };
        }

        public static IQueryable<CheckTaskOrg> Search(long? taskId, long? orgId)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (orgId.IsNotEmpty())        q = q.Where(o => o.OrgId == orgId.Value);
            return q;
        }
    }

    //==================================================================
    /// <summary>检查任务-要检查的对象</summary>
    public class CheckTaskObject : EntityBase<CheckTaskObject>
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查对象")] public long? ObjectId { get; set; }
        [UI("是否检查")] public bool? IsChecked { get; set;}

        // Relations
        public virtual CheckTask Task { get; set; }
        public virtual CheckObject Object { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TaskId,
                TaskName = Task?.Name,
                ObjectId,
                ObjectName = Object?.Name,
                IsChecked
            };
        }

        public static IQueryable<CheckTaskObject> Search(long? taskId)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            return q;
        }
    }

    //==================================================================
    /// <summary>检查任务-检查表关联</summary>
    public class CheckTaskSheet: EntityBase<CheckTaskSheet>
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查表")] public long? SheetId { get; set; }

        // Relations
        public virtual CheckTask Task { get; set; }
        public virtual CheckSheet Sheet { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TaskId,
                TaskName = Task?.Name,
                SheetId,
                SheetName = Sheet?.Name
            };
        }

        public static IQueryable<CheckTaskSheet> Search(long? taskId, long? sheetId)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (sheetId.IsNotEmpty()) q = q.Where(o => o.SheetId == sheetId.Value);
            return q;
        }
    }
}
