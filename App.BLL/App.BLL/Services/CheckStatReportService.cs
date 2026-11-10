using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;
using OrgEntity = App.DAL.Org;

namespace App.Services
{
    /// <summary>检查统计报表行。</summary>
    public class CheckStatReportRow : IExport
    {
        [UI("区域Id", Column = ColumnType.None)] public long OrgId { get; set; }
        [UI("排序", Column = ColumnType.None)] public int SortId { get; set; }
        [UI("检查开始时间", Column = ColumnType.None)] public string CheckStartDt { get; set; }
        [UI("检查结束时间", Column = ColumnType.None)] public string CheckEndDt { get; set; }
        [UI("隐患开始时间", Column = ColumnType.None)] public string HazardStart { get; set; }
        [UI("隐患结束时间", Column = ColumnType.None)] public string HazardEnd { get; set; }
        [UI("区域名称", ColumnWidth = 24)] public string OrgName { get; set; }
        [UI("纳管对象数", ColumnWidth = 14)] public int ObjectCount { get; set; }
        [UI("检查次数", ColumnWidth = 12)] public int CheckCount { get; set; }

        [UI("一般隐患数(已整改数)", ColumnWidth = 20)] public string NormalHazardText => FormatPair(NormalHazardCount, NormalHazardFixedCount);
        [UI("重大隐患数(已整改数)", ColumnWidth = 20)] public string HighHazardText => FormatPair(HighHazardCount, HighHazardFixedCount);
        [UI("重点问题数(已整改数)", ColumnWidth = 20)] public string HighProblemText => FormatPair(HighProblemCount, HighProblemFixedCount);
        [UI("隐患总数(已整改数)", ColumnWidth = 18)] public string TotalHazardText => FormatPair(TotalHazardCount, TotalHazardFixedCount);
        [UI("每千次巡查发现隐患数", ColumnWidth = 18)] public string HazardPerThousandText => FormatNumber(HazardPerThousand);
        [UI("重大隐患、重点问题发现率", ColumnWidth = 22)] public string MajorProblemRateText => FormatPercent(MajorProblemRate);

        [UI("一般隐患数", Column = ColumnType.None)] public int NormalHazardCount { get; set; }
        [UI("一般隐患已整改数", Column = ColumnType.None)] public int NormalHazardFixedCount { get; set; }
        [UI("重大隐患数", Column = ColumnType.None)] public int HighHazardCount { get; set; }
        [UI("重大隐患已整改数", Column = ColumnType.None)] public int HighHazardFixedCount { get; set; }
        [UI("重点问题数", Column = ColumnType.None)] public int HighProblemCount { get; set; }
        [UI("重点问题已整改数", Column = ColumnType.None)] public int HighProblemFixedCount { get; set; }
        [UI("隐患总数", Column = ColumnType.None)] public int TotalHazardCount { get; set; }
        [UI("隐患总已整改数", Column = ColumnType.None)] public int TotalHazardFixedCount { get; set; }
        [UI("每千次巡查发现隐患数原值", Column = ColumnType.None)] public decimal HazardPerThousand { get; set; }
        [UI("重大隐患重点问题发现率原值", Column = ColumnType.None)] public decimal MajorProblemRate { get; set; }

        /// <summary>导出报表行。</summary>
        public object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                OrgName,
                ObjectCount,
                CheckCount,
                NormalHazardText,
                HighHazardText,
                HighProblemText,
                TotalHazardText,
                HazardPerThousandText,
                MajorProblemRateText,
            };
        }

        /// <summary>格式化计数对文本。</summary>
        static string FormatPair(int total, int fixedCount)
        {
            return $"{total}({fixedCount})";
        }

        /// <summary>格式化数值文本。</summary>
        static string FormatNumber(decimal value)
        {
            return value.ToString("0.##");
        }

        /// <summary>格式化百分比文本。</summary>
        static string FormatPercent(decimal value)
        {
            return $"{value:0.##}%";
        }
    }

    /// <summary>检查统计报表服务。</summary>
    public static class CheckStatReportService
    {
        /// <summary>构建指定根网格的统计行。</summary>
        public static List<CheckStatReportRow> GetRows(long rootOrgId, DateTime startDt, DateTime endDt, HashSet<long> visibleOrgIds = null)
        {
            var allOrgs = OrgEntity.All
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList();
            if (allOrgs.Count == 0)
                return new List<CheckStatReportRow>();

            visibleOrgIds ??= allOrgs.Select(t => t.Id).ToHashSet();
            if (!visibleOrgIds.Contains(rootOrgId))
                return new List<CheckStatReportRow>();

            var root = allOrgs.FirstOrDefault(t => t.Id == rootOrgId);
            if (root == null)
                return new List<CheckStatReportRow>();

            var rowOrgs = BuildRowOrgs(allOrgs, root, visibleOrgIds);
            if (rowOrgs.Count == 0)
                return new List<CheckStatReportRow>();

            var subtreeOrgIds = allOrgs
                .GetDescendants(rootOrgId)
                .Select(t => t.Id)
                .Where(visibleOrgIds.Contains)
                .ToHashSet();
            var objectCounts = LoadObjectCounts(subtreeOrgIds);
            var checkCounts = LoadCheckCounts(subtreeOrgIds, startDt, endDt);
            var hazardCounts = LoadHazardCounts(subtreeOrgIds, startDt, endDt);

            var rows = new List<CheckStatReportRow>(rowOrgs.Count);
            for (var i = 0; i < rowOrgs.Count; i++)
            {
                var org = rowOrgs[i];
                var ids = allOrgs
                    .GetDescendants(org.Id)
                    .Select(t => t.Id)
                    .Where(visibleOrgIds.Contains)
                    .ToList();
                var row = BuildRow(org, ids, objectCounts, checkCounts, hazardCounts);
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>构建当前行展示的组织列表。</summary>
        static List<OrgEntity> BuildRowOrgs(List<OrgEntity> allOrgs, OrgEntity root, HashSet<long> visibleOrgIds)
        {
            var rows = new List<OrgEntity> { root };
            var children = allOrgs
                .Where(t => t.ParentId == root.Id && visibleOrgIds.Contains(t.Id))
                .OrderBy(t => t.SortId)
                .ThenBy(t => t.Id)
                .ToList();
            rows.AddRange(children);
            return rows;
        }

        /// <summary>加载纳管对象计数。</summary>
        static Dictionary<long, int> LoadObjectCounts(HashSet<long> orgIds)
        {
            var orgIdList = orgIds.ToList();
            return CheckObject.Set
                .AsNoTracking()
                .Where(t => t.OrgId.HasValue && orgIdList.Contains(t.OrgId.Value))
                .Where(t => !t.IsDel.HasValue || !t.IsDel.Value)
                .GroupBy(t => t.OrgId.Value)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToDictionary(t => t.OrgId, t => t.Count);
        }

        /// <summary>加载检查次数计数。</summary>
        static Dictionary<long, int> LoadCheckCounts(HashSet<long> orgIds, DateTime startDt, DateTime endDt)
        {
            var orgIdList = orgIds.ToList();
            return Check.Set
                .AsNoTracking()
                .Where(t => t.CheckDt.HasValue && t.CheckDt.Value >= startDt && t.CheckDt.Value <= endDt)
                .Where(t => t.OrgId.HasValue && orgIdList.Contains(t.OrgId.Value))
                .GroupBy(t => t.OrgId.Value)
                .Select(g => new { OrgId = g.Key, Count = g.Count() })
                .ToDictionary(t => t.OrgId, t => t.Count);
        }

        /// <summary>加载隐患计数。</summary>
        static Dictionary<long, HazardAgg> LoadHazardCounts(HashSet<long> orgIds, DateTime startDt, DateTime endDt)
        {
            var orgIdList = orgIds.ToList();
            var list = CheckHazard.Set
                .AsNoTracking()
                .Where(t => t.Check != null && t.Check.CheckDt.HasValue && t.Check.CheckDt.Value >= startDt && t.Check.CheckDt.Value <= endDt)
                .Where(t => t.OrgId.HasValue && orgIdList.Contains(t.OrgId.Value))
                .Select(t => new
                {
                    OrgId = t.OrgId.Value,
                    Level = t.CheckItem.HazardLevel,
                    t.Status,
                })
                .ToList();

            var dict = new Dictionary<long, HazardAgg>();
            foreach (var item in list)
            {
                if (!dict.TryGetValue(item.OrgId, out var agg))
                {
                    agg = new HazardAgg();
                    dict[item.OrgId] = agg;
                }
                agg.Add(item.Level, item.Status);
            }
            return dict;
        }

        /// <summary>构建单行统计结果。</summary>
        static CheckStatReportRow BuildRow(
            OrgEntity org,
            List<long> subtreeIds,
            IReadOnlyDictionary<long, int> objectCounts,
            IReadOnlyDictionary<long, int> checkCounts,
            IReadOnlyDictionary<long, HazardAgg> hazardCounts)
        {
            var row = new CheckStatReportRow
            {
                OrgId = org.Id,
                SortId = org.SortId,
                OrgName = org.Name,
                ObjectCount = SumCounts(subtreeIds, objectCounts),
                CheckCount = SumCounts(subtreeIds, checkCounts),
            };

            foreach (var orgId in subtreeIds)
            {
                if (!hazardCounts.TryGetValue(orgId, out var agg))
                    continue;
                row.NormalHazardCount += agg.NormalCount;
                row.NormalHazardFixedCount += agg.NormalFixedCount;
                row.HighHazardCount += agg.HighCount;
                row.HighHazardFixedCount += agg.HighFixedCount;
                row.HighProblemCount += agg.ProblemCount;
                row.HighProblemFixedCount += agg.ProblemFixedCount;
            }

            row.TotalHazardCount = row.NormalHazardCount + row.HighHazardCount + row.HighProblemCount;
            row.TotalHazardFixedCount = row.NormalHazardFixedCount + row.HighHazardFixedCount + row.HighProblemFixedCount;

            row.HazardPerThousand = CalcRate(row.TotalHazardCount, row.CheckCount, 1000m);
            row.MajorProblemRate = CalcRate(row.HighHazardCount + row.HighProblemCount, row.TotalHazardCount, 100m);
            return row;
        }

        /// <summary>汇总整型计数。</summary>
        static int SumCounts(IEnumerable<long> ids, IReadOnlyDictionary<long, int> counts)
        {
            var sum = 0;
            foreach (var id in ids)
            {
                if (counts.TryGetValue(id, out var count))
                    sum += count;
            }
            return sum;
        }

        /// <summary>计算比率。</summary>
        static decimal CalcRate(int numerator, int denominator, decimal factor)
        {
            if (denominator <= 0 || numerator <= 0)
                return 0m;
            return Math.Round(numerator * factor / denominator, 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>隐患聚合桶。</summary>
        sealed class HazardAgg
        {
            public int NormalCount { get; private set; }
            public int NormalFixedCount { get; private set; }
            public int HighCount { get; private set; }
            public int HighFixedCount { get; private set; }
            public int ProblemCount { get; private set; }
            public int ProblemFixedCount { get; private set; }
            public int TotalCount { get; private set; }
            public int TotalFixedCount { get; private set; }

            /// <summary>累加一条隐患。</summary>
            public void Add(CheckHazardLevel? level, CheckHazardStatus? status)
            {
                var fixedCount = IsFixed(status);
                TotalCount++;
                if (fixedCount)
                    TotalFixedCount++;

                if (level == CheckHazardLevel.NormalHazard)
                {
                    NormalCount++;
                    if (fixedCount)
                        NormalFixedCount++;
                }
                else if (level == CheckHazardLevel.HighHazard)
                {
                    HighCount++;
                    if (fixedCount)
                        HighFixedCount++;
                }
                else if (level == CheckHazardLevel.HighProblem)
                {
                    ProblemCount++;
                    if (fixedCount)
                        ProblemFixedCount++;
                }
            }

            /// <summary>判断是否已整改。</summary>
            static bool IsFixed(CheckHazardStatus? status)
            {
                return status == CheckHazardStatus.Fixed || status == CheckHazardStatus.Archived;
            }
        }
    }
}
