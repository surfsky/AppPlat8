using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    //==================================================================
    /// <summary>检查任务-要检查的对象</summary>
    public class CheckTaskObject : EntityBase<CheckTaskObject>, ISort
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查对象")] public long? ObjectId { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("是否已完成")] public bool IsFinished { get; set;} = false;



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
                IsFinished
            };
        }

        public static IQueryable<CheckTaskObject> Search(long? taskId, bool? isFinished=null, long? objectId=null)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (isFinished.IsNotEmpty())   q = q.Where(o => o.IsFinished == isFinished.Value);
            if (objectId.IsNotEmpty())     q = q.Where(o => o.ObjectId == objectId.Value);
            return q;
        }
    }
}
