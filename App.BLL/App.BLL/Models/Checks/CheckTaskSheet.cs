using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
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
