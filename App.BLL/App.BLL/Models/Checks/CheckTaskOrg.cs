using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
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
}
