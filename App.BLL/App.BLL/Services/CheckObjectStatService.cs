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
        public object Export(ExportMode mode = ExportMode.Normal)
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

        // ---------- 公共缓存入口（60 秒滑动窗口）----------
        public static List<CheckObjectStatRow> GetStat(DateTime startDt, DateTime endDt)
        {
            var key = $"CheckObjectStat:{startDt:yyyyMMdd}-{endDt:yyyyMMdd}";
            var expire = DateTime.Now.AddSeconds(60);
            return Cacher.Get(key, () => ComputeCore(startDt, endDt), expire);
        }

        // ---------- 统计核心 ----------
        static List<CheckObjectStatRow> ComputeCore(DateTime startDt, DateTime endDt)
        {
            // 1. 每一行的组织（含子）Id 集合
            var rowOrgIds = _sections
                .Select(s => s.IncludeSub
                    ? Org.GetChildIds(s.OrgId) ?? new List<long> { s.OrgId }
                    : new List<long> { s.OrgId })
                .Select(ids => ids.ToHashSet())
                .ToList();

            // 2. 所有对象的 TagId 集合字典（1 SQL 查 CheckObjectTag，避免每条子查询）
            var objTags = CheckObjectTag.Set
                .AsNoTracking()
                .Select(t => new { t.CheckObjectId, t.TagId })
                .ToList()
                .GroupBy(t => t.CheckObjectId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TagId).ToHashSet());

            // 3. 取全部检查对象（含 IsDel=true 的也拿，统计关停/新增区间时要用）
            var all = CheckObject.Set
                .AsNoTracking()
                .Select(o => new
                {
                    o.Id,
                    o.IsDel,
                    o.CreateDt,
                    o.DeleteDt,
                    o.DutyOrgId,
                    o.RiskLevel,
                    o.Scale,
                })
                .ToList();

            DateTime start = startDt.Date;
            DateTime end   = endDt.Date.AddDays(1).AddTicks(-1);
            bool InRange(DateTime? dt) => dt.HasValue && dt.Value >= start && dt.Value <= end;

            static bool HasTag(IDictionary<long, HashSet<long>> dict, long oid, long tid)
                => dict.TryGetValue(oid, out var s) && s.Contains(tid);
            static bool HasAny(IDictionary<long, HashSet<long>> dict, long oid, IEnumerable<long> tids)
            {
                if (!dict.TryGetValue(oid, out var s)) return false;
                foreach (var t in tids) if (s.Contains(t)) return true;
                return false;
            }

            // 4. 逐行累加
            var rows = new List<CheckObjectStatRow>(_sections.Length);
            for (int i = 0; i < _sections.Length; i++)
            {
                var s = _sections[i];
                var orgSet = rowOrgIds[i];
                var row = new CheckObjectStatRow
                {
                    Index         = i + 1,
                    SectionName   = s.Name,
                    SectionRemark = s.Remark,
                };
                foreach (var o in all)
                {
                    var inThis = o.DutyOrgId.HasValue && orgSet.Contains(o.DutyOrgId.Value);
                    if (!inThis) continue;

                    // 基础计数 / 新增 / 关停
                    if (!o.IsDel.GetValueOrDefault()) row.BaseCount++;
                    if (InRange(o.CreateDt))          row.NewCount++;
                    if (InRange(o.DeleteDt))          row.CloseCount++;

                    var oid = o.Id;
                    // 厂房类型 × 集聚区内/外
                    var isInArea  = HasTag(objTags, oid, 97);
                    var isOutArea = HasTag(objTags, oid, 98);
                    if (HasTag(objTags, oid, 93)) { if (isInArea) row.FactoryIndependentInside++;  else if (isOutArea) row.FactoryIndependentOutside++;  }
                    if (HasTag(objTags, oid, 95)) { if (isInArea) row.FactoryWorkshopInside++;     else if (isOutArea) row.FactoryWorkshopOutside++;     }
                    if (HasTag(objTags, oid, 94)) { if (isInArea) row.FactoryParkInside++;         else if (isOutArea) row.FactoryParkOutside++;         }

                    // 产值规模（优先 CheckObject.Scale 枚举，空值时再回退到 TagId 79/88/99）
                    if      (o.Scale == CheckObjectScale.AboveScaleYi)   row.ScaleAboveBillion++;
                    else if (o.Scale == CheckObjectScale.AboveScale)     row.ScaleBelowBillion++;
                    else if (o.Scale == CheckObjectScale.BelowScale)     row.ScaleSmall++;
                    else if (o.Scale == CheckObjectScale.SmallMicro)     row.ScaleMicro++;
                    else
                    {
                        if      (HasTag(objTags, oid, 79)) row.ScaleAboveBillion++;
                        else if (HasTag(objTags, oid, 88)) row.ScaleBelowBillion++;
                        else if (HasTag(objTags, oid, 99)) row.ScaleSmall++;   // TagId 99 暂映射为"规下"；若后续有专门"小微"TagId 可拆分
                    }

                    // 印刷
                    if (HasTag(objTags, oid, 108)) row.IndPrintJiaoYin++;
                    if (HasTag(objTags, oid, 153)) row.IndPrintAoBan++;
                    if (HasAny(objTags, oid, new long[]{145, 154})) row.IndPrintOther++;
                    if (HasTag(objTags, oid, 108) || HasTag(objTags, oid, 153) || HasAny(objTags, oid, new long[]{145,154}))
                        row.IndPrint++;

                    // 塑料制品行业
                    if (HasTag(objTags, oid, 118)) row.IndPlasticWoven++;
                    if (HasTag(objTags, oid, 119)) row.IndPlasticOther++;
                    if (HasTag(objTags, oid, 110) || HasTag(objTags, oid, 118) || HasTag(objTags, oid, 119))
                        row.IndPlastic++;

                    // 12 行业
                    if (HasTag(objTags, oid, 109)) row.IndPaper++;
                    if (HasTag(objTags, oid, 117)) { row.IndEpe++;    row.RiskSponge++; row.RiskTextile++; } // 珍珠棉 → 3 个行业桶
                    if (HasTag(objTags, oid, 112)) row.IndMechanic++;
                    if (HasTag(objTags, oid, 113)) row.IndMetal++;
                    if (HasTag(objTags, oid, 114)) row.IndTrade++;
                    if (HasTag(objTags, oid, 116)) row.IndOther++;
                    if (HasAny(objTags, oid, new long[]{116, 108, 109, 110, 111, 112, 113, 114, 115, 117, 118, 119})) { /* noop */ }
                    if (HasTag(objTags, oid, 115)) row.RiskAppliance += 0; // 家电 116 已占，预留列
                    if (HasTag(objTags, oid, 148)) row.RiskElectroplating++;
                    if (HasTag(objTags, oid, 149)) row.RiskBamboo++;
                    if (HasTag(objTags, oid, 150)) row.RiskShoe++;
                    if (HasTag(objTags, oid, 151)) row.RiskSponge++;
                    if (HasAny(objTags, oid, new long[]{111, 117})) row.RiskTextile++;

                    // 八类高危
                    if (HasTag(objTags, oid, 111)) row.RiskSprayPaint++;
                    if (HasTag(objTags, oid, 152)) row.RiskInkPrint++;
                    if (HasTag(objTags, oid, 120)) row.RiskConfinedSpace++;
                    if (HasTag(objTags, oid, 121)) row.RiskDust++;
                    if (HasTag(objTags, oid, 122)) row.RiskPainting++;
                    if (HasTag(objTags, oid, 123)) row.RiskMetalSmelt++;
                    if (HasTag(objTags, oid, 124)) row.RiskAmmonia++;
                    if (HasTag(objTags, oid, 125)) row.RiskShipBuilding++;

                    // 三场所三企业
                    if (HasTag(objTags, oid, 84)) row.RiskChangSuo++;

                    // 关注风险
                    if (HasTag(objTags, oid, 126)) row.FocusChemical++;
                    if (HasTag(objTags, oid, 127)) row.FocusInflammable++;
                    if (HasTag(objTags, oid, 86))  row.FocusEnvProtection++;
                    if (HasTag(objTags, oid, 8))   row.FocusMine++;

                    // 四色等级（用 CheckRiskLevel 枚举）
                    switch (o.RiskLevel)
                    {
                        case CheckRiskLevel.None:   row.ColorNone++;   break;
                        case CheckRiskLevel.Low:    row.ColorLow++;    break;
                        case CheckRiskLevel.Medium: row.ColorMedium++; break;
                        case CheckRiskLevel.High:   row.ColorHigh++;   break;
                        default:                    row.ColorNone++;   break;
                    }
                }
                rows.Add(row);
            }
            return rows;
        }
    }
}
