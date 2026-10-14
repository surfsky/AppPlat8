using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    //==================================================================
    // CheckTask ---(1:n)--- CheckTaskHazard
    //==================================================================
    /// <summary>检查任务-要检查的隐患（尚未启用）</summary>
    public class CheckTaskHazard : EntityBase<CheckTaskHazard>, ISort
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查隐患")] public long? HazardId { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("是否已完成")] public bool IsFinished { get; set;} = false;
        [UI("OrgID")] public long? OrgId { get; set; }  // 冗余设计（数据来自ObjectId)，便于查询


        // Relations
        public virtual CheckTask Task { get; set; }
        public virtual CheckHazard Hazard { get; set; }
       public virtual Org Org { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TaskId,
                TaskName = Task?.Name,
                HazardId,
                HazardDescription = Hazard?.Description,
                ObjectId = Hazard?.ObjectId,
                ObjectName = Hazard?.Object?.Name,
                SortId,
                IsFinished,
                OrgId,
                OrgName = Org?.Name,
            };
        }


        public static IQueryable<CheckTaskHazard> Search(long? taskId=null, bool? isFinished=null, long? objectId=null, long? orgId=null)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (isFinished.IsNotEmpty())   q = q.Where(o => o.IsFinished == isFinished.Value);
            if (objectId.IsNotEmpty())     q = q.Where(o => o.Hazard.ObjectId == objectId.Value);
            if (orgId.IsNotEmpty())        q = q.Where(o => o.OrgId == orgId.Value);
            return q;
        }
    }
}
