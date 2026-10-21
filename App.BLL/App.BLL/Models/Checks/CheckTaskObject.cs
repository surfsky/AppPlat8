using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    //==================================================================
    // CheckTask ---(1:n)--- CheckTaskObject
    //==================================================================
    /// <summary>检查任务-要检查的对象</summary>
    public class CheckTaskObject : EntityBase<CheckTaskObject>, ISort, IFix<CheckTaskObject>
    {
        [UI("任务")] public long? TaskId { get; set; }
        [UI("检查对象")] public long? CheckObjectId { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("是否已完成")] public bool IsFinished { get; set;} = false;
        [UI("OrgID")] public long? OrgId { get; set; }  // 冗余设计（数据来自CheckObjectId)，便于查询
        [UI("CheckerId")] public long? CheckerId { get; set; }  // 检查人ID，冗余设计，便于查询


        // Relations
        public virtual CheckTask Task { get; set; }
        public virtual CheckObject CheckObject { get; set; }
        public virtual Org Org { get; set; }
        public virtual User Checker { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TaskId,
                TaskName = Task?.Name,
                CheckObjectId,
                ObjectName = CheckObject?.Name,
                IsFinished,
                StatusName = IsFinished ? "已完成" : "未完成",
                OrgId,
                OrgName = Org?.Name,
                CheckerId,
                CheckerName = Checker?.Name,
            };
        }

        public static IQueryable<CheckTaskObject> Search(long? taskId=null, bool? isFinished=null, long? objectId=null, long? orgId=null, long? checkerId=null)
        {
            var q = IncludeSet.AsQueryable();
            if (taskId.IsNotEmpty())       q = q.Where(o => o.TaskId == taskId.Value);
            if (isFinished.IsNotEmpty())   q = q.Where(o => o.IsFinished == isFinished.Value);
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
    }
}
