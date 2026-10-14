using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    //==================================================================
    // CheckTask ---(1:n)--- CheckTaskObject
    //==================================================================
    /// <summary>检查任务-要检查的对象</summary>
    public class CheckTaskObject : EntityBase<CheckTaskObject>, ISort
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查对象")] public long? ObjectId { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("是否已完成")] public bool IsFinished { get; set;} = false;
        [UI("OrgID")] public long? OrgId { get; set; }  // 冗余设计（数据来自ObjectId)，便于查询


        // Relations
        public virtual CheckTask Task { get; set; }
        public virtual CheckObject Object { get; set; }
        public virtual Org Org { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TaskId,
                TaskName = Task?.Name,
                ObjectId,
                ObjectName = Object?.Name,
                IsFinished,
                OrgId,
                OrgName = Org?.Name,
            };
        }

        public static IQueryable<CheckTaskObject> Search(long? taskId=null, bool? isFinished=null, long? objectId=null, long? orgId=null)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (isFinished.IsNotEmpty())   q = q.Where(o => o.IsFinished == isFinished.Value);
            if (objectId.IsNotEmpty())     q = q.Where(o => o.ObjectId == objectId.Value);
            if (orgId.IsNotEmpty())        q = q.Where(o => o.OrgId == orgId.Value);
            return q;
        }

        /// <summary>保存前处理(若 OrgId 为空，则从 ObjectId 中获取)</summary>
        public override void BeforeSave(EntityOp op)
        {
            base.BeforeSave(op);
            if (this.ObjectId.IsNotEmpty() && this.OrgId.IsEmpty())
            {
                this.OrgId = CheckObject.Get(this.ObjectId)?.DutyOrgId;
            }
        }
    }
}
