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
    /// <summary>按网格员细分的专项任务统计报表行。</summary>
    public class CheckTaskStat2ReportRow : IExport
    {
        [UI("行键", Column = ColumnType.None)] public string RowKey => $"{OrgId}_{CheckerId?.ToString() ?? "0"}";
        [UI("任务Id", Column = ColumnType.None)] public long TaskId { get; set; }
        [UI("组织Id", Column = ColumnType.None)] public long OrgId { get; set; }
        [UI("排序", Column = ColumnType.None)] public int SortId { get; set; }
        [UI("网格员Id", Column = ColumnType.None)] public long? CheckerId { get; set; }
        [UI("组织科室", ColumnWidth = 22)] public string OrgName { get; set; }
        [UI("网格员", ColumnWidth = 16)] public string CheckerName { get; set; }

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
                CheckerName,
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

    /// <summary>按组织及网格员维度统计专项任务。</summary>
    public static class CheckTaskStat2ReportService
    {
        sealed class TaskObjectStat
        {
            public long? OrgId { get; set; }
            public long? CheckObjectId { get; set; }
            public long? CheckerId { get; set; }
            public CheckTaskObjectStatus? Status { get; set; }
        }

        sealed class HazardStat
        {
            public long? OrgId { get; set; }
            public long? CheckObjectId { get; set; }
            public CheckHazardStatus? Status { get; set; }
        }

        /// <summary>生成根组织及其直属组织的网格员分项统计行。</summary>
        public static List<CheckTaskStat2ReportRow> GetRows(long taskId, long rootOrgId, long? checkerId = null, HashSet<long> visibleOrgIds = null)
        {
            if (taskId <= 0 || rootOrgId <= 0)
                return new List<CheckTaskStat2ReportRow>();

            var allOrgs = OrgEntity.All.OrderBy(t => t.SortId).ThenBy(t => t.Id).ToList();
            if (allOrgs.Count == 0)
                return new List<CheckTaskStat2ReportRow>();

            visibleOrgIds ??= allOrgs.Select(t => t.Id).ToHashSet();
            if (!visibleOrgIds.Contains(rootOrgId))
                return new List<CheckTaskStat2ReportRow>();

            var root = allOrgs.FirstOrDefault(t => t.Id == rootOrgId);
            if (root == null)
                return new List<CheckTaskStat2ReportRow>();

            var rowOrgs = new List<OrgEntity> { root };
            rowOrgs.AddRange(allOrgs
                .Where(t => t.ParentId == root.Id && visibleOrgIds.Contains(t.Id))
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id));

            var subtreeOrgIds = allOrgs.GetDescendants(rootOrgId)
                .Select(t => t.Id)
                .Where(visibleOrgIds.Contains)
                .ToHashSet();

            var taskObjects = CheckTaskObject.Set
                .AsNoTracking()
                .Where(t => t.TaskId == taskId)
                .Select(t => new TaskObjectStat
                {
                    OrgId = t.OrgId ?? t.CheckObject.OrgId,
                    CheckObjectId = t.CheckObjectId,
                    CheckerId = t.CheckerId ?? t.CheckObject.CheckerId,
                    Status = t.Status,
                })
                .ToList()
                .Where(t => t.OrgId.HasValue && subtreeOrgIds.Contains(t.OrgId.Value))
                .ToList();

            var objectIds = taskObjects.Where(t => t.CheckObjectId.HasValue)
                .Select(t => t.CheckObjectId.Value).Distinct().ToList();
            var hazards = objectIds.Count == 0
                ? new List<HazardStat>()
                : CheckHazard.Set.AsNoTracking()
                    .Where(h => h.CheckObjectId.HasValue && objectIds.Contains(h.CheckObjectId.Value))
                    .Select(h => new HazardStat
                    {
                        OrgId = h.OrgId ?? h.CheckObject.OrgId,
                        CheckObjectId = h.CheckObjectId,
                        Status = h.Status,
                    })
                    .ToList()
                    .Where(h => h.OrgId.HasValue && subtreeOrgIds.Contains(h.OrgId.Value))
                    .ToList();

            var checkerIds = taskObjects.Select(t => t.CheckerId)
                .Where(t => t.HasValue && t.Value > 0)
                .Select(t => t.Value)
                .Distinct()
                .ToList();
            if (checkerId.HasValue && checkerId.Value > 0)
                checkerIds.Add(checkerId.Value);
            var checkerNames = checkerIds.Distinct().Count() == 0
                ? new Dictionary<long, string>()
                : User.Set.AsNoTracking()
                    .Where(t => checkerIds.Contains(t.Id))
                    .Select(t => new { t.Id, t.RealName, t.Name })
                    .ToDictionary(t => t.Id, t => t.RealName.IsNotEmpty() ? t.RealName.Trim() : t.Name?.Trim() ?? "");

            var rows = new List<CheckTaskStat2ReportRow>();
            foreach (var org in rowOrgs)
            {
                var orgIds = allOrgs.GetDescendants(org.Id)
                    .Select(t => t.Id)
                    .Where(visibleOrgIds.Contains)
                    .ToHashSet();
                var scopedObjects = taskObjects
                    .Where(t => t.OrgId.HasValue && orgIds.Contains(t.OrgId.Value))
                    .ToList();
                var rowCheckerIds = checkerId.HasValue
                    ? (scopedObjects.Any(t => t.CheckerId == checkerId.Value)
                        ? new List<long?> { checkerId.Value }
                        : new List<long?>())
                    : scopedObjects.Select(t => t.CheckerId).Distinct()
                        .OrderBy(t => !t.HasValue)
                        .ThenBy(t => t.HasValue && checkerNames.TryGetValue(t.Value, out var name) ? name : "")
                        .ThenBy(t => t ?? 0)
                        .ToList();
                if (checkerId.HasValue && rowCheckerIds.Count == 0)
                    continue;
                if (rowCheckerIds.Count == 0)
                    rowCheckerIds.Add(null);

                foreach (var rowCheckerId in rowCheckerIds)
                {
                    var checkerObjects = scopedObjects.Where(t => t.CheckerId == rowCheckerId).ToList();
                    var required = checkerObjects.Count;
                    var checkedCount = checkerObjects.Count(t => t.Status == CheckTaskObjectStatus.Checked || t.Status == CheckTaskObjectStatus.Finished);
                    var fixedCount = checkerObjects.Count(t => t.Status == CheckTaskObjectStatus.Finished);
                    var checkerObjectIds = checkerObjects.Where(t => t.CheckObjectId.HasValue)
                        .Select(t => t.CheckObjectId.Value).ToHashSet();
                    var scopedHazards = hazards.Where(h => h.OrgId.HasValue && orgIds.Contains(h.OrgId.Value)
                        && h.CheckObjectId.HasValue && checkerObjectIds.Contains(h.CheckObjectId.Value)).ToList();
                    var foundHazards = scopedHazards.Count;
                    var fixedHazards = scopedHazards.Count(h => IsFixed(h.Status));

                    rows.Add(new CheckTaskStat2ReportRow
                    {
                        TaskId = taskId,
                        OrgId = org.Id,
                        SortId = org.SortId,
                        CheckerId = rowCheckerId,
                        OrgName = org.Name,
                        CheckerName = rowCheckerId.HasValue && checkerNames.TryGetValue(rowCheckerId.Value, out var checkerName)
                            ? checkerName : "",
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
            }
            return rows;
        }

        static bool IsFixed(CheckHazardStatus? status)
        {
            return status == CheckHazardStatus.Fixed || status == CheckHazardStatus.Archived;
        }

        static decimal Rate(long numerator, long denominator)
        {
            if (numerator <= 0 || denominator <= 0)
                return 0m;
            return Math.Round((decimal)numerator * 100m / denominator, 2, MidpointRounding.AwayFromZero);
        }
    }
}