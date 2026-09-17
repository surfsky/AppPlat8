using System;
using System.Collections.Generic;
using System.Linq;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

namespace App.Services
{
    /// <summary>企业分类统计行（对应截图 6 行：应急局/新城/城北/城西/城中/协调）</summary>
    public class CheckObjectStatRow : IExport
    {
        [UI("序号")]          public int    Index            { get; set; }
        [UI("组织科室")]      public string SectionName      { get; set; }
        [UI("组织说明")]      public string SectionRemark    { get; set; }
        [UI("企业数-个数")]   public int    BaseCount        { get; set; }
        [UI("新增企业")]      public int    NewCount         { get; set; }
        [UI("关停企业")]      public int    CloseCount       { get; set; }

        // --- 厂房类型 × 集聚区内/外 ---
        [UI("厂房-独立厂房-区内")]  public int FactoryIndependentInside   { get; set; }
        [UI("厂房-独立厂房-区外")]  public int FactoryIndependentOutside  { get; set; }
        [UI("厂房-厂中厂-区内")]    public int FactoryWorkshopInside      { get; set; }
        [UI("厂房-厂中厂-区外")]    public int FactoryWorkshopOutside     { get; set; }
        [UI("厂房-园中园-区内")]    public int FactoryParkInside          { get; set; }
        [UI("厂房-园中园-区外")]    public int FactoryParkOutside         { get; set; }

        // --- 产值规模 ---
        [UI("产值-规上亿+")]       public int ScaleAboveBillion  { get; set; }
        [UI("产值-规上亿-")]       public int ScaleBelowBillion  { get; set; }
        [UI("产值-规下企业")]      public int ScaleSmall         { get; set; }
        [UI("产值-小微企业")]      public int ScaleMicro         { get; set; }
        [UI("产值-规下及小微合计")] public int ScaleSmallMicro   { get { return ScaleSmall + ScaleMicro; } set { /* IExport 兼容 */ } }

        // --- 印刷行业 ---
        [UI("印刷-胶印")]          public int IndPrintJiaoYin        { get; set; }
        [UI("印刷-凹版")]          public int IndPrintAoBan          { get; set; }
        [UI("印刷-其他印刷")]      public int IndPrintOther          { get; set; }

        // --- 塑料制品行业 ---
        [UI("塑料-塑编")]          public int IndPlasticWoven        { get; set; }
        [UI("塑料-其他塑料")]      public int IndPlasticOther        { get; set; }

        // --- 行业分类（11 类核心） ---
        [UI("行业-纸制品业")]      public int IndPaper            { get; set; }
        [UI("行业-珍珠棉")]        public int IndEpe               { get; set; }
        [UI("行业-印刷行业")]      public int IndPrint             { get; set; }
        [UI("行业-塑料制品业")]    public int IndPlastic           { get; set; }
        [UI("行业-机械行业")]      public int IndMechanic          { get; set; }
        [UI("行业-金属制品业")]    public int IndMetal             { get; set; }
        [UI("行业-商贸行业")]      public int IndTrade             { get; set; }
        [UI("行业-其他行业")]      public int IndOther             { get; set; }

        // --- 八类危险性较高工业企业 ---
        [UI("高危-家电制造")]            public int RiskAppliance         { get; set; }
        [UI("高危-电镀类")]              public int RiskElectroplating    { get; set; }
        [UI("高危-竹木加工")]            public int RiskBamboo            { get; set; }
        [UI("高危-制鞋类")]              public int RiskShoe              { get; set; }
        [UI("高危-海绵生产")]            public int RiskSponge            { get; set; }
        [UI("高危-纺织印染")]            public int RiskTextile           { get; set; }
        [UI("高危-喷漆(非水性)")]        public int RiskSprayPaint       { get; set; }
        [UI("高危-油墨印刷(非水性)")]     public int RiskInkPrint         { get; set; }


        // --- 三场所三企业 ---
        [UI("三场所三企业")]              public int RiskChangSuo         { get; set; }
        [UI("高危-有限空间作业")]          public int RiskConfinedSpace    { get; set; }
        [UI("高危-可燃爆粉尘作业场所")]     public int RiskDust             { get; set; }
        [UI("高危-喷涂作业场所")]          public int RiskPainting         { get; set; }
        [UI("高危-金属冶炼企业")]          public int RiskMetalSmelt       { get; set; }
        [UI("高危-涉氨制冷企业")]          public int RiskAmmonia          { get; set; }
        [UI("高危-船舶修造企业")]          public int RiskShipBuilding     { get; set; }

        // --- 关注风险 ---
        [UI("关注-使用危险化学品企业")]   public int FocusChemical        { get; set; }
        [UI("关注-易燃企业")]           public int FocusInflammable     { get; set; }
        [UI("关注-有环保设备")]         public int FocusEnvProtection   { get; set; }
        [UI("关注-非煤矿山")]           public int FocusMine            { get; set; }

        // --- 分级管控四色等级 ---
        [UI("四色-白名单(None)")]    public int ColorNone   { get; set; }
        [UI("四色-黄(Low)")]        public int ColorLow    { get; set; }
        [UI("四色-橙(Medium)")]     public int ColorMedium { get; set; }
        [UI("四色-红(High)")]       public int ColorHigh   { get; set; }

        /// <summary>IExport 接口实现：供 SortPageExport/ExcelExporter 使用</summary>
        public virtual object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Index, SectionName, SectionRemark,
                BaseCount, NewCount, CloseCount,
                FactoryIndependentInside, FactoryIndependentOutside,
                FactoryWorkshopInside, FactoryWorkshopOutside,
                FactoryParkInside, FactoryParkOutside,
                ScaleAboveBillion, ScaleBelowBillion, ScaleSmall, ScaleMicro, ScaleSmallMicro,
                PrintJiaoYin = IndPrintJiaoYin,
                PrintAoBan = IndPrintAoBan,
                PrintOther = IndPrintOther,
                PlasticWoven = IndPlasticWoven,
                PlasticOther = IndPlasticOther,
                IndPaper, IndEpe, IndPrint, IndPlastic, IndMechanic, IndMetal,
                IndTrade, IndOther,
                IndAppliance = RiskAppliance,
                IndElectroplating = RiskElectroplating,
                IndBamboo = RiskBamboo,
                IndShoe = RiskShoe,
                IndSponge = RiskSponge,
                IndTextile = RiskTextile,
                RiskSprayPaint, RiskInkPrint, RiskConfinedSpace, RiskDust,
                RiskPainting, RiskMetalSmelt, RiskAmmonia, RiskShipBuilding,
                SanChangSuo = RiskChangSuo,
                FocusChemical, FocusInflammable, FocusEnvProtection, FocusMine,
                FourColorNone = ColorNone,
                FourColorLow = ColorLow,
                FourColorMedium = ColorMedium,
                FourColorHigh = ColorHigh,
            };
        }
    }

    /// <summary>网格员对象统计行（在 CheckObjectStatRow 基础上增加网格员两列）</summary>
    public class CheckObjectCheckerStatRow : CheckObjectStatRow
    {
        [UI("网格员Id")]   public long?  CheckerId    { get; set; }
        [UI("网格员")]     public string CheckerName  { get; set; }

        public override object Export(ExportMode mode = ExportMode.Normal)
        {
            var b = (dynamic)base.Export(mode);
            return new
            {
                Index,
                SectionName,
                SectionRemark,
                CheckerId,
                CheckerName,
                BaseCount, NewCount, CloseCount,
                FactoryIndependentInside, FactoryIndependentOutside,
                FactoryWorkshopInside, FactoryWorkshopOutside,
                FactoryParkInside, FactoryParkOutside,
                ScaleAboveBillion, ScaleBelowBillion, ScaleSmall, ScaleMicro, ScaleSmallMicro,
                PrintJiaoYin = IndPrintJiaoYin,
                PrintAoBan = IndPrintAoBan,
                PrintOther = IndPrintOther,
                PlasticWoven = IndPlasticWoven,
                PlasticOther = IndPlasticOther,
                IndPaper, IndEpe, IndPrint, IndPlastic, IndMechanic, IndMetal,
                IndTrade, IndOther,
                IndAppliance = RiskAppliance,
                IndElectroplating = RiskElectroplating,
                IndBamboo = RiskBamboo,
                IndShoe = RiskShoe,
                IndSponge = RiskSponge,
                IndTextile = RiskTextile,
                RiskSprayPaint, RiskInkPrint, RiskConfinedSpace, RiskDust,
                RiskPainting, RiskMetalSmelt, RiskAmmonia, RiskShipBuilding,
                SanChangSuo = RiskChangSuo,
                FocusChemical, FocusInflammable, FocusEnvProtection, FocusMine,
                FourColorNone = ColorNone,
                FourColorLow = ColorLow,
                FourColorMedium = ColorMedium,
                FourColorHigh = ColorHigh,
            };
        }
    }

    /// <summary>企业分类统计服务（对应「工矿企业分类统计表」截图）</summary>
    public static class CheckObjectStatService
    {
        // 截图 6 个科室行的组织 Id 及分组方式（是否包含子组织）
        static readonly (long OrgId, string Name, string Remark, bool IncludeSub)[] _sections = new[]
        {
            ( 23L, "应急局", "(OrgId=23 包含子组织，以下同)",       true  ),
            ( 24L, "新城",   "(OrgId=24)",                          true  ),
            (129L, "城北",   "(OrgId=129)",                         true  ),
            (126L, "城西",   "(OrgId=126)",                         true  ),
            ( 73L, "城中",   "(OrgId=73)",                          true  ),
            (327L, "协调",   "(OrgId=327)",                         true  ),
        };

        // 网格员报表：只统计 5 个科室（不包含应急局汇总行）
        static readonly (long OrgId, string Name, string Remark, bool IncludeSub)[] _checkerSections = new[]
        {
            ( 24L, "新城",   "(OrgId=24)",   true ),
            (129L, "城北",   "(OrgId=129)",  true ),
            (126L, "城西",   "(OrgId=126)",  true ),
            ( 73L, "城中",   "(OrgId=73)",   true ),
            (327L, "协调",   "(OrgId=327)",  true ),
        };

        // ---------- 公共缓存入口（60 秒滑动窗口）----------
        public static List<CheckObjectStatRow> GetStat(DateTime startDt, DateTime endDt)
        {
            var key = $"CheckObjectStat:{startDt:yyyyMMdd}-{endDt:yyyyMMdd}";
            var expire = DateTime.Now.AddSeconds(60);
            return Cacher.Get(key, () => ComputeCore(startDt, endDt), expire);
        }

        // ---------- 网格员 × 科室 统计入口 ----------
        public static List<CheckObjectCheckerStatRow> GetCheckerStat(DateTime startDt, DateTime endDt)
        {
            var key = $"CheckObjectCheckerStat:{startDt:yyyyMMdd}-{endDt:yyyyMMdd}";
            var expire = DateTime.Now.AddSeconds(60);
            return Cacher.Get(key, () => ComputeCheckerCore(startDt, endDt), expire);
        }

        // ---------- 统计核心（公共：把 CheckObject + TagId 全部加载 + 每行列值枚举）----------
        static List<CheckObjectStatRow> ComputeCore(DateTime startDt, DateTime endDt)
        {
            var ctx = BuildStatContext();
            DateTime start = startDt.Date;
            DateTime end   = endDt.Date.AddDays(1).AddTicks(-1);
            bool InRange(DateTime? dt) => dt.HasValue && dt.Value >= start && dt.Value <= end;

            var rows = new List<CheckObjectStatRow>(_sections.Length);
            for (int i = 0; i < _sections.Length; i++)
            {
                var s = _sections[i];
                var orgSet = s.IncludeSub
                    ? Org.GetChildIds(s.OrgId)?.ToHashSet() ?? new HashSet<long> { s.OrgId }
                    : new HashSet<long> { s.OrgId };

                var row = new CheckObjectStatRow
                {
                    Index         = i + 1,
                    SectionName   = s.Name,
                    SectionRemark = s.Remark,
                };
                AccumulateRow(row, ctx, orgSet, InRange, o => true);
                rows.Add(row);
            }
            return rows;
        }

        static List<CheckObjectCheckerStatRow> ComputeCheckerCore(DateTime startDt, DateTime endDt)
        {
            var ctx = BuildStatContext();
            DateTime start = startDt.Date;
            DateTime end   = endDt.Date.AddDays(1).AddTicks(-1);
            bool InRange(DateTime? dt) => dt.HasValue && dt.Value >= start && dt.Value <= end;

            var checkerIds = ctx.Objects
                .Select(o => o.CheckerId)
                .Where(id => id.HasValue && id.Value > 0)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var checkerNameMap = App.DAL.User.ValidSet
                .AsNoTracking()
                .Where(u => checkerIds.Contains(u.Id))
                .Select(u => new { u.Id, Name = u.RealName })
                .ToDictionary(x => x.Id, x => x.Name);

            var rows = new List<CheckObjectCheckerStatRow>();
            int idx = 0;
            foreach (var s in _checkerSections)
            {
                var orgSet = s.IncludeSub
                    ? Org.GetChildIds(s.OrgId)?.ToHashSet() ?? new HashSet<long> { s.OrgId }
                    : new HashSet<long> { s.OrgId };

                // 该科室下有对象的 CheckerId（distinct 升序）
                var sectionCheckerIds = new List<long>();
                foreach (var o in ctx.Objects)
                {
                    if (!o.DutyOrgId.HasValue || !orgSet.Contains(o.DutyOrgId.Value)) continue;
                    if (o.CheckerId.HasValue && o.CheckerId.Value > 0 && !sectionCheckerIds.Contains(o.CheckerId.Value))
                        sectionCheckerIds.Add(o.CheckerId.Value);
                }
                sectionCheckerIds.Sort();

                // 只生成该科室 scope 内有 CheckerId 的行
                foreach (var cid in sectionCheckerIds)
                {
                    var checkerRow = new CheckObjectCheckerStatRow
                    {
                        Index         = ++idx,
                        SectionName   = $"{s.Name}",
                        SectionRemark = $"根据 CheckObjectStat.DutyOrgId= 统计",
                        CheckerId     = cid,
                        CheckerName   = checkerNameMap.TryGetValue(cid, out var cn) && !string.IsNullOrWhiteSpace(cn) ? cn : $"ID={cid}",
                    };
                    AccumulateRow(checkerRow, ctx, orgSet, InRange, o => o.CheckerId.HasValue && o.CheckerId.Value == cid);
                    if (checkerRow.BaseCount > 0 || checkerRow.NewCount > 0 || checkerRow.CloseCount > 0)
                        rows.Add(checkerRow);
                }
            }
            return rows;
        }

        // ---------- 内部辅助 ----------
        sealed class StatContext
        {
            public List<ObjLite> Objects;
            public Dictionary<long, HashSet<long>> ObjTags;
        }
        sealed class ObjLite
        {
            public long Id; public bool? IsDel; public DateTime? CreateDt; public DateTime? DeleteDt;
            public long? DutyOrgId; public long? CheckerId; public CheckRiskLevel? RiskLevel; public CheckObjectScale? Scale;
        }

        static StatContext BuildStatContext()
        {
            var objTags = CheckObjectTag.Set
                .AsNoTracking()
                .Select(t => new { t.CheckObjectId, t.TagId })
                .ToList()
                .GroupBy(t => t.CheckObjectId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TagId).ToHashSet());

            var all = CheckObject.Set
                .AsNoTracking()
                .Select(o => new ObjLite
                {
                    Id          = o.Id,
                    IsDel       = o.IsDel,
                    CreateDt    = o.CreateDt,
                    DeleteDt    = o.DeleteDt,
                    DutyOrgId   = o.DutyOrgId,
                    CheckerId   = o.CheckerId,
                    RiskLevel   = o.RiskLevel,
                    Scale       = o.Scale,
                })
                .ToList();

            return new StatContext { Objects = all, ObjTags = objTags };
        }

        static void AccumulateRow(CheckObjectStatRow row, StatContext ctx, HashSet<long> orgSet, Func<DateTime?, bool> inRange, Func<ObjLite, bool> extraWhere)
        {
            static bool HasTag(IDictionary<long, HashSet<long>> dict, long oid, long tid)
                => dict.TryGetValue(oid, out var s) && s.Contains(tid);
            static bool HasAny(IDictionary<long, HashSet<long>> dict, long oid, IEnumerable<long> tids)
            {
                if (!dict.TryGetValue(oid, out var s)) return false;
                foreach (var t in tids) if (s.Contains(t)) return true;
                return false;
            }

            foreach (var o in ctx.Objects)
            {
                var inThis = o.DutyOrgId.HasValue && orgSet.Contains(o.DutyOrgId.Value);
                if (!inThis) continue;
                if (!extraWhere(o)) continue;

                if (!o.IsDel.GetValueOrDefault()) row.BaseCount++;
                if (inRange(o.CreateDt))          row.NewCount++;
                if (inRange(o.DeleteDt))          row.CloseCount++;

                var oid = o.Id;
                var isInArea  = HasTag(ctx.ObjTags, oid, 97);
                var isOutArea = HasTag(ctx.ObjTags, oid, 98);
                if (HasTag(ctx.ObjTags, oid, 93)) { if (isInArea) row.FactoryIndependentInside++;  else if (isOutArea) row.FactoryIndependentOutside++;  }
                if (HasTag(ctx.ObjTags, oid, 95)) { if (isInArea) row.FactoryWorkshopInside++;     else if (isOutArea) row.FactoryWorkshopOutside++;     }
                if (HasTag(ctx.ObjTags, oid, 94)) { if (isInArea) row.FactoryParkInside++;         else if (isOutArea) row.FactoryParkOutside++;         }

                if      (o.Scale == CheckObjectScale.AboveScaleYi)   row.ScaleAboveBillion++;
                else if (o.Scale == CheckObjectScale.AboveScale)     row.ScaleBelowBillion++;
                else if (o.Scale == CheckObjectScale.BelowScale)     row.ScaleSmall++;
                else if (o.Scale == CheckObjectScale.SmallMicro)     row.ScaleMicro++;
                else
                {
                    if      (HasTag(ctx.ObjTags, oid, 79)) row.ScaleAboveBillion++;
                    else if (HasTag(ctx.ObjTags, oid, 88)) row.ScaleBelowBillion++;
                    else if (HasTag(ctx.ObjTags, oid, 99)) row.ScaleSmall++;
                }

                if (HasTag(ctx.ObjTags, oid, 108)) row.IndPrintJiaoYin++;
                if (HasTag(ctx.ObjTags, oid, 153)) row.IndPrintAoBan++;
                if (HasAny(ctx.ObjTags, oid, new long[]{145, 154})) row.IndPrintOther++;
                if (HasTag(ctx.ObjTags, oid, 108) || HasTag(ctx.ObjTags, oid, 153) || HasAny(ctx.ObjTags, oid, new long[]{145,154}))
                    row.IndPrint++;

                if (HasTag(ctx.ObjTags, oid, 118)) row.IndPlasticWoven++;
                if (HasTag(ctx.ObjTags, oid, 119)) row.IndPlasticOther++;
                if (HasTag(ctx.ObjTags, oid, 110) || HasTag(ctx.ObjTags, oid, 118) || HasTag(ctx.ObjTags, oid, 119))
                    row.IndPlastic++;

                if (HasTag(ctx.ObjTags, oid, 109)) row.IndPaper++;
                if (HasTag(ctx.ObjTags, oid, 117)) { row.IndEpe++;    row.RiskSponge++; row.RiskTextile++; }
                if (HasTag(ctx.ObjTags, oid, 112)) row.IndMechanic++;
                if (HasTag(ctx.ObjTags, oid, 113)) row.IndMetal++;
                if (HasTag(ctx.ObjTags, oid, 114)) row.IndTrade++;
                if (HasTag(ctx.ObjTags, oid, 116)) row.IndOther++;
                if (HasTag(ctx.ObjTags, oid, 148)) row.RiskElectroplating++;
                if (HasTag(ctx.ObjTags, oid, 149)) row.RiskBamboo++;
                if (HasTag(ctx.ObjTags, oid, 150)) row.RiskShoe++;
                if (HasTag(ctx.ObjTags, oid, 151)) row.RiskSponge++;
                if (HasAny(ctx.ObjTags, oid, new long[]{111, 117})) row.RiskTextile++;

                if (HasTag(ctx.ObjTags, oid, 111)) row.RiskSprayPaint++;
                if (HasTag(ctx.ObjTags, oid, 152)) row.RiskInkPrint++;
                if (HasTag(ctx.ObjTags, oid, 120)) row.RiskConfinedSpace++;
                if (HasTag(ctx.ObjTags, oid, 121)) row.RiskDust++;
                if (HasTag(ctx.ObjTags, oid, 122)) row.RiskPainting++;
                if (HasTag(ctx.ObjTags, oid, 123)) row.RiskMetalSmelt++;
                if (HasTag(ctx.ObjTags, oid, 124)) row.RiskAmmonia++;
                if (HasTag(ctx.ObjTags, oid, 125)) row.RiskShipBuilding++;

                if (HasTag(ctx.ObjTags, oid, 84)) row.RiskChangSuo++;

                if (HasTag(ctx.ObjTags, oid, 126)) row.FocusChemical++;
                if (HasTag(ctx.ObjTags, oid, 127)) row.FocusInflammable++;
                if (HasTag(ctx.ObjTags, oid, 86))  row.FocusEnvProtection++;
                if (HasTag(ctx.ObjTags, oid, 8))   row.FocusMine++;

                switch (o.RiskLevel)
                {
                    case CheckRiskLevel.None:   row.ColorNone++;   break;
                    case CheckRiskLevel.Low:    row.ColorLow++;    break;
                    case CheckRiskLevel.Medium: row.ColorMedium++; break;
                    case CheckRiskLevel.High:   row.ColorHigh++;   break;
                    default:                    row.ColorNone++;   break;
                }
            }
        }
    }
}
