using System;
using System.Collections.Generic;
using System.Linq;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;
using OrgEntity = App.DAL.Org;

namespace App.Services
{
    /// <summary>专项任务统计报表行。</summary>
    public class CheckTaskStatReportRow : IExport
    {
        [UI("任务Id", Column = ColumnType.None)] public long TaskId { get; set; }
        [UI("组织Id", Column = ColumnType.None)] public long OrgId { get; set; }
        [UI("排序", Column = ColumnType.None)] public int SortId { get; set; }
        [UI("组织科室", ColumnWidth = 22)] public string OrgName { get; set; }

        [UI("企业/应排查企业数", ColumnWidth = 16)] public long RequiredCompanyCount { get; set; }
        [UI("企业/已排查企业数", ColumnWidth = 16)] public long CheckedCompanyCount { get; set; }
        [UI("企业/已整改企业数", ColumnWidth = 16)] public long FixedCompanyCount { get; set; }
        [UI("企业/排查进度（%）", ColumnWidth = 15)] public decimal CheckProgress { get; set; }

        [UI("隐患/发现隐患数", ColumnWidth = 15)] public long FoundHazardCount { get; set; }
        [UI("隐患/已整改隐患数", ColumnWidth = 16)] public long FixedHazardCount { get; set; }
        [UI("隐患/隐患发现率", ColumnWidth = 14)] public decimal HazardFoundRate { get; set; }
        [UI("隐患/隐患整改率", ColumnWidth = 14)] public decimal HazardFixedRate { get; set; }

        public object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                OrgName,
                RequiredCompanyCount,
                CheckedCompanyCount,
                FixedCompanyCount,
                CheckProgress,
                FoundHazardCount,
                FixedHazardCount,
                HazardFoundRate,
                HazardFixedRate,
            };
        }
    }

    /// <summary>专项任务统计报表服务。</summary>
    public static class CheckTaskStatReportService
    {
        sealed class TaskObjectStat
        {
            public long? OrgId { get; set; }
            public long? CheckObjectId { get; set; }
            public CheckTaskObjectStatus? Status { get; set; }
        }

        sealed class HazardStat
        {
            public long? OrgId { get; set; }
            public CheckHazardStatus? Status { get; set; }
        }

        /// <summary>按所选专项任务统计根组织及其直属组织；各行均汇总自身及可见下级组织。</summary>
        public static List<CheckTaskStatReportRow> GetRows(long taskId, long rootOrgId, HashSet<long> visibleOrgIds = null)
        {
            if (taskId <= 0 || rootOrgId <= 0)
                return new List<CheckTaskStatReportRow>();

            var allOrgs = OrgEntity.All
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList();
            if (allOrgs.Count == 0)
                return new List<CheckTaskStatReportRow>();

            visibleOrgIds ??= allOrgs.Select(t => t.Id).ToHashSet();
            if (!visibleOrgIds.Contains(rootOrgId))
                return new List<CheckTaskStatReportRow>();

            var root = allOrgs.FirstOrDefault(t => t.Id == rootOrgId);
            if (root == null)
                return new List<CheckTaskStatReportRow>();

            var rowOrgs = new List<OrgEntity> { root };
            rowOrgs.AddRange(allOrgs
                .Where(t => t.ParentId == root.Id && visibleOrgIds.Contains(t.Id))
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id));

            var subtreeOrgIds = allOrgs
                .GetDescendants(rootOrgId)
                .Select(t => t.Id)
                .Where(visibleOrgIds.Contains)
                .ToHashSet();
            if (subtreeOrgIds.Count == 0)
                return new List<CheckTaskStatReportRow>();

            var taskObjects = CheckTaskObject.Set
                .AsNoTracking()
                .Where(t => t.TaskId == taskId)
                .Select(t => new TaskObjectStat
                {
                    OrgId = t.OrgId ?? t.CheckObject.OrgId,
                    CheckObjectId = t.CheckObjectId,
                    Status = t.Status,
                })
                .ToList()
                .Where(t => t.OrgId.HasValue && subtreeOrgIds.Contains(t.OrgId.Value))
                .ToList();

            var objectIds = taskObjects
                .Where(t => t.CheckObjectId.HasValue)
                .Select(t => t.CheckObjectId.Value)
                .Distinct()
                .ToList();

            var hazards = objectIds.Count == 0
                ? new List<HazardStat>()
                : CheckHazard.Set
                    .AsNoTracking()
                    .Where(h => h.CheckObjectId.HasValue && objectIds.Contains(h.CheckObjectId.Value))
                    .Select(h => new HazardStat
                    {
                        OrgId = h.OrgId ?? h.CheckObject.OrgId,
                        Status = h.Status,
                    })
                    .ToList()
                    .Where(h => h.OrgId.HasValue && subtreeOrgIds.Contains(h.OrgId.Value))
                    .ToList();

            var rows = new List<CheckTaskStatReportRow>(rowOrgs.Count);
            for (var index = 0; index < rowOrgs.Count; index++)
            {
                var org = rowOrgs[index];
                var ids = allOrgs
                    .GetDescendants(org.Id)
                    .Select(t => t.Id)
                    .Where(visibleOrgIds.Contains)
                    .ToHashSet();

                var scopedObjects = taskObjects.Where(t => t.OrgId.HasValue && ids.Contains(t.OrgId.Value)).ToList();
                var scopedHazards = hazards.Where(t => t.OrgId.HasValue && ids.Contains(t.OrgId.Value)).ToList();
                var required = scopedObjects.Count;
                var checkedCount = scopedObjects.Count(t => t.Status == CheckTaskObjectStatus.Checked || t.Status == CheckTaskObjectStatus.Finished);
                var fixedCount = scopedObjects.Count(t => t.Status == CheckTaskObjectStatus.Finished);
                var foundHazards = scopedHazards.Count;
                var fixedHazards = scopedHazards.Count(t => IsFixed(t.Status));

                rows.Add(new CheckTaskStatReportRow
                {
                    TaskId = taskId,
                    OrgId = org.Id,
                    SortId = org.SortId,
                    OrgName = org.Name,
                    RequiredCompanyCount = required,
                    CheckedCompanyCount = checkedCount,
                    FixedCompanyCount = fixedCount,
                    CheckProgress = Rate(checkedCount, required),
                    FoundHazardCount = foundHazards,
                    FixedHazardCount = fixedHazards,
                    HazardFoundRate = Rate(foundHazards, checkedCount),
                    HazardFixedRate = Rate(fixedHazards, foundHazards),
                });
            }
            return rows;
        }

        static bool IsFixed(CheckHazardStatus? status)
        {
            return status == CheckHazardStatus.Fixed
                || status == CheckHazardStatus.Archived;
        }

        static decimal Rate(long numerator, long denominator)
        {
            if (numerator <= 0 || denominator <= 0)
                return 0m;
            return Math.Round((decimal)numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);
        }
    }
}
