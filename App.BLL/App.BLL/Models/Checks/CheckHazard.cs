using System;
using System.Collections.Generic;
using System.Linq;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

/**
检查记录表CheckLog --(1:n)-- CheckHazard 隐患表 --(1:n)--CheckHazardReview 隐患复查记录表
*/
namespace App.DAL
{
    //-------------------------------------------------------
    // 其他枚举值
    //-------------------------------------------------------
    // Waiting -> Processing -> Finished -> Monitor -> Archived
    [UI("检查", "隐患状态")]
    public enum CheckHazardStatus
    {
        [UI("新建")] Waiting = 0,
        [UI("整改中")] Processing = 1,
        [UI("已整改")] Finished = 2,
        [UI("督查中")] Monitor = 3,
        [UI("已归档")] Archived = 9,
    }

    // 隐患整改方式（1：直接整改，2：复查）
    [UI("检查", "隐患整改方式")]
    public enum CheckHazardRectifyType
    {
        [UI("立查立改")] Direct = 1,
        [UI("限期整改")] Limit = 2,
    }

    //-------------------------------------------------------
    /// 隐患表(CheckHazard----(1:n)----CheckHazardLog)
    //-------------------------------------------------------
    [UI("检查", "隐患")]
    public class CheckHazard : EntityBase<CheckHazard>
    {
        [UI("检查对象")] public long? CheckObjectId { get; set; }    // 冗余存储，方便查询
        [UI("检查人员")] public long? CheckerId { get; set; }   // 冗余存储，方便查询
        [UI("检查记录")] public long? CheckLogId { get; set; }
        [UI("检查表Id")] public long? CheckSheetId { get; set; }
        [UI("检查项Id")] public long? CheckItemId { get; set; }
        [UI("检查项内容")] public string CheckItemText { get; set; } // 冗余存储，防止检查项删除
        [UI("隐患描述")] public string Description { get; set; }     // 隐患详情
        [UI("整改状态")] public CheckHazardStatus? Status { get; set; } = CheckHazardStatus.Waiting;
        [UI("整改期限")] public DateTime? ExpireDt { get; set; }
        [UI("整改日期")] public DateTime? RectifyDt { get; set; }
        [UI("整改方式")] public CheckHazardRectifyType? RectifyType { get; set; } = CheckHazardRectifyType.Limit;

        //
        [UI("录入141")] public bool? IsIn141 { get; set; }
        [UI("常见隐患")] public bool? IsCommonHazard { get; set; }

        //
        public virtual CheckObject CheckObject { get; set; }
        public virtual User Checker { get; set; }
        public virtual Check Check { get; set; }
        public virtual CheckSheet CheckSheet { get; set; }
        public virtual CheckSheetItem CheckItem { get; set; }
        public virtual List<CheckHazardLog> Logs { get; set; }

        //
        public string StatusName => Status.GetTitle();
        public string ObjectName => CheckObject?.Name;
        public string OrgName => CheckObject?.OrgName;
        public string CheckerName => Checker?.Name;
        public string CheckSheetName => CheckSheet?.Name;
        public string CheckItemName => CheckItem?.Name;
        public CheckHazardLevel? HazardLevel  => CheckItem?.HazardLevel;
        public string HazardLevelName => HazardLevel.GetTitle();
        public string RectifyTypeName => RectifyType.GetTitle();



        //
        public override object Export(ExportMode mode)
        {
            return new
            {
                Id,
                CheckObjectId,
                CheckerId,
                CheckLogId,
                CheckItemId,
                CheckItemText,
                Description,
                this.ImageUrls,
                //Images,
                Status,
                CreateDt,
                CreateDtText = CreateDt?.ToString("yyyy-MM-dd HH:mm"),
                ExpireDt,
                ExpireDtText = ExpireDt?.ToString("yyyy-MM-dd HH:mm"),
                RectifyDt,
                RectifyDtText = RectifyDt?.ToString("yyyy-MM-dd HH:mm"),
                IsIn141,
                RectifyType,
                RectifyTypeName,

                StatusName,
                ObjectName,
                CheckerName,
                CheckSheetName,
                CheckItemName,
                HazardLevel,
                HazardLevelName = HazardLevel.GetTitle(),
                CheckDtText = Check?.CheckDt?.ToString("yyyy-MM-dd HH:mm"),
                DutyOrgName = OrgName,
            };
        }
        public static IQueryable<CheckHazard> Search(
            string objectName, long? objectId, 
            string checkerName, long? checkerId, CheckHazardStatus? status, 
            DateTime? createStartDt, DateTime? expireTo = null, 
            long? orgId = null, List<long> orgIds = null)
        {
            IQueryable<CheckHazard> q = CheckHazard.IncludeSet.Include(t => t.CheckObject).Include(t => t.Checker).Include(t => t.CheckItem).Include(t => t.Check);
            if (objectId.IsNotEmpty())          q = q.Where(o => o.CheckObjectId == objectId.Value);
            else if (objectName.IsNotEmpty())   q = q.Where(o => o.CheckObject.Name.Contains(objectName.Trim()));
            if (checkerId.IsNotEmpty())         q = q.Where(o => o.CheckerId == checkerId.Value);
            else if (checkerName.IsNotEmpty())  q = q.Where(o => o.Checker.Name.Contains(checkerName.Trim()));
            if (status.IsNotEmpty())            q = q.Where(o => o.Status == status.Value);
            var dutyNetIds = GetOrgIds(orgIds, orgId);
            if (dutyNetIds.Count > 0)            q = q.Where(o => o.CheckObject != null && o.CheckObject.OrgId.HasValue && dutyNetIds.Contains(o.CheckObject.OrgId.Value));
            if (expireTo.IsNotEmpty())          q = q.Where(o => o.ExpireDt.HasValue && o.ExpireDt.Value <= expireTo.Value.Date);
            if (createStartDt.IsNotEmpty())     q = q.Where(o => o.CreateDt >= createStartDt.Value.Date);
            return q;
        }

        private static List<long> GetOrgIds(List<long> orgIds, long? orgId)
        {
            var list = new List<long>();
            if (orgIds != null && orgIds.Count > 0) list.AddRange(orgIds);
            if (orgId.IsNotEmpty()) list.Add(orgId.Value);
            list = list.Distinct().ToList();
            if (list.Count == 0) return new List<long>();
            return Org.All
                .GetDescendants(list)
                .Select(t => t.Id)
                .Distinct()
                .ToList();
        }
    }
}