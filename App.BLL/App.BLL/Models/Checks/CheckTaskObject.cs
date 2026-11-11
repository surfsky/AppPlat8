using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{

    public enum CheckTaskObjectStatus
    {
        [UI("未检查")] Unchecked = 0,
        [UI("已检查")] Checked = 1,
        [UI("已完成整改")] Finished = 2
    }

    //==================================================================
    // CheckTask ---(1:n)--- CheckTaskObject
    //==================================================================
    /// <summary>检查任务-要检查的对象</summary>
    public class CheckTaskObject : Entity<CheckTaskObject>, ISort, IFix<CheckTaskObject>
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查对象")] public long? CheckObjectId { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("检查状态")] public CheckTaskObjectStatus? Status { get; set; } = CheckTaskObjectStatus.Unchecked;
        [UI("发现隐患数")] public long? HazardCnt { get; set; }
        [UI("已整改隐患数")] public long? FixHazardCnt { get; set; }
        [UI("OrgID")] public long? OrgId { get; set; }  // 冗余设计（数据来自CheckObjectId)，便于查询
        [UI("CheckerId")] public long? CheckerId { get; set; }  // 检查人ID，冗余设计，便于查询


        // Relations
        public virtual CheckTask Task { get; set; }
        public virtual CheckObject CheckObject { get; set; }
        public virtual Org Org { get; set; }
        public virtual User Checker { get; set; }

        public string TaskName => Task?.Name;
        public string ObjectName => CheckObject?.Name;
        public string OrgName => Org?.Name ?? CheckObject?.OrgName;
        public string CheckerName => Checker?.RealName.IsNotEmpty() == true ? Checker.RealName : Checker?.Name;

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TaskId,
                TaskName,
                CheckObjectId,
                ObjectName,
                Status,
                StatusName = Status.GetTitle(),
                HazardCnt,
                FixHazardCnt,
                OrgId,
                OrgName,
                CheckerId,
                CheckerName,
            };
        }

        public static IQueryable<CheckTaskObject> Search(long? taskId=null, CheckTaskObjectStatus? status=null, long? objectId=null, long? orgId=null, long? checkerId=null)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (status.IsNotEmpty())       q = q.Where(o => o.Status == status.Value);
            if (objectId.IsNotEmpty())     q = q.Where(o => o.CheckObjectId == objectId.Value);
            if (orgId.IsNotEmpty())        q = q.Where(o => o.OrgId == orgId.Value || o.CheckObject.OrgId == orgId.Value);
            if (checkerId.IsNotEmpty())    q = q.Where(o => o.CheckerId == checkerId.Value || o.CheckObject.CheckerId == checkerId.Value);
            return q;
        }

        /// <summary>保存前处理(若 OrgId 为空，则从 CheckObjectId 中获取)</summary>
        public override void BeforeSave(EntityOp op)
        {
            base.BeforeSave(op);
            Fix();
        }

        /// <summary>修复数据(若 OrgId 为空，则从 CheckObjectId 中获取)</summary>
        public CheckTaskObject Fix()
        {
            if (this.CheckObjectId.IsNotEmpty() && this.OrgId.IsEmpty())
            {
                this.OrgId = this.CheckObject?.OrgId;
                this.CheckerId = this.CheckObject?.CheckerId;
            }
            return this;
        }

        /// <summary>刷新指定检查对象在所有任务中的发现隐患数和已整改隐患数。</summary>
        public static void RefreshHazardCounts(long checkObjectId)
        {
            if (checkObjectId <= 0) return;

            var hazards = CheckHazard.Set.Where(h => h.CheckObjectId == checkObjectId);
            var hazardCount = hazards.LongCount();
            var fixedHazardCount = hazards.LongCount(h =>
                h.Status == CheckHazardStatus.Fixed
                || h.Status == CheckHazardStatus.Monitor
                || h.Status == CheckHazardStatus.Archived);

            var taskObjects = CheckTaskObject.Set.Where(t => t.CheckObjectId == checkObjectId).ToList();
            foreach (var taskObject in taskObjects)
            {
                var status = taskObject.Status;
                if (hazardCount > 0)
                    status = fixedHazardCount == hazardCount
                        ? CheckTaskObjectStatus.Finished
                        : CheckTaskObjectStatus.Checked;

                if (taskObject.HazardCnt == hazardCount
                    && taskObject.FixHazardCnt == fixedHazardCount
                    && taskObject.Status == status) continue;
                taskObject.HazardCnt = hazardCount;
                taskObject.FixHazardCnt = fixedHazardCount;
                taskObject.Status = status;
                taskObject.Save();
            }
        }
    }
}
