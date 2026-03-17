using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /*
    检查任务
    CheckTask --1:n--CheckTaskOrg（受理组织）
              --1:n--CheckTaskObject（要检查的对象）
              --1:n--CheckTaskSheet（检查单）
    */
    [UI("检查", "检查任务")]
    public class CheckTask : EntityBase<CheckTask>
    {
        [UI("父任务")] public long? ParentId { get; set; }
        [UI("名称")] public string Name { get; set; }
        [UI("发布人")] public long? PublisherId { get; set; }
        [UI("备注")] public string Remark { get; set; }
        [UI("截止时间")] public DateTime? ExpireDt { get; set; }
        [UI("进度")] public float? Progress { get; set; }


        // Relations
        public virtual CheckTask Parent { get; set; }
        public virtual List<CheckTask> Children { get; set; }
        public virtual User Publisher { get; set; }
        public virtual List<CheckTaskOrg> Orgs { get; set; }
        public virtual List<CheckTaskObject> CheckObjects { get; set; }
        public virtual List<CheckTaskSheet> CheckSheets { get; set; }


        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                PublisherId,
                PublisherName = Publisher?.Name,
                Remark,
                CreateDt,
                ExpireDt,
                Progress
            };
        }

        public static IQueryable<CheckTask> Search(string name, long? publisherId, DateTime? expireBefore)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())          q = q.Where(o => o.Name.Contains(name.Trim()));
            if (publisherId.IsNotEmpty())   q = q.Where(o => o.PublisherId == publisherId.Value);
            if (expireBefore.IsNotEmpty())  q = q.Where(o => o.ExpireDt <= expireBefore.Value);
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


    /// <summary>
    /// 检查任务-组织关联
    /// </summary>
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

    /// <summary>
    /// 检查任务-要检查的对象
    /// </summary>
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

    /// <summary>
    /// 检查任务-检查表关联
    /// </summary>
    public class CheckTaskSheet: EntityBase<CheckTaskSheet>
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查表")] public long? CheckSheetId { get; set; }

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
                CheckSheetId,
                CheckSheetName = Sheet?.Name
            };
        }

        public static IQueryable<CheckTaskSheet> Search(long? taskId, long? checkSheetId)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (checkSheetId.IsNotEmpty()) q = q.Where(o => o.CheckSheetId == checkSheetId.Value);
            return q;
        }
    }
}
