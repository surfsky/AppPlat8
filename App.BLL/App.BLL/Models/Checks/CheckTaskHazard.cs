using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    //==================================================================
    /// <summary>检查任务-要检查的隐患</summary>
    public class CheckTaskHazard : EntityBase<CheckTaskHazard>, ISort
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查隐患")] public long? HazardId { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("是否已完成")] public bool IsFinished { get; set;} = false;


        // Relations
        public virtual CheckTask Task { get; set; }
        public virtual CheckHazard Hazard { get; set; }

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
                IsFinished
            };
        }


        public static IQueryable<CheckTaskHazard> Search(long? taskId=null, bool? isFinished=null, long? objectId=null)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (isFinished.IsNotEmpty())   q = q.Where(o => o.IsFinished == isFinished.Value);
            if (objectId.IsNotEmpty())     q = q.Where(o => o.Hazard.ObjectId == objectId.Value);
            return q;
        }
    }
}
