using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;

namespace App.Services
{
    //----------------------------------------------------------------------
    // 企业分类统计行
    //----------------------------------------------------------------------
    /// <summary>企业分类统计行</summary>
    public class CheckObjectStatRow : IExport
    {
        [UI("序号")]                                    public int    Index            { get; set; }
        [UI("组织科室")]                                public string SectionName      { get; set; }
        [UI("组织说明",  Column = ColumnType.None)]     public string SectionRemark    { get; set; }
        [UI("组织科室Id", Column = ColumnType.None)]    public long?  OrgId            { get; set; }
        [UI("统计开始时间", Column = ColumnType.None)]  public DateTime StartDt        { get; set; }
        [UI("统计结束时间", Column = ColumnType.None)]  public DateTime EndDt          { get; set; }

        // --- 企业数 ---
        [UI("企业数", "个数")]                                  public int BaseCount      { get; set; }
        [UI("企业数", "新增企业")]                              public int NewCount       { get; set; }
        [UI("企业数", "关停企业")]                              public int CloseCount     { get; set; }

        // --- 厂房类型 ---
        [UI("厂房类型/独立厂房",   "集居区内", Tag ="93,97")]                 public int FactoryIndependentInside   { get; set; }
        [UI("厂房类型/独立厂房",   "集居区外", Tag ="93,98")]                 public int FactoryIndependentOutside  { get; set; }
        [UI("厂房类型/厂中厂",     "集居区内", Tag ="95,97")]                 public int FactoryWorkshopInside      { get; set; }
        [UI("厂房类型/厂中厂",     "集居区外", Tag ="95,98")]                 public int FactoryWorkshopOutside     { get; set; }
        [UI("厂房类型/园中园",     "集居区内", Tag ="94,97")]                 public int FactoryParkInside          { get; set; }
        [UI("厂房类型/园中园",     "集居区外", Tag ="94,98")]                 public int FactoryParkOutside         { get; set; }

        // --- 产值规模 ---
        [UI("产值规模", "规上企业(亿元以上)", Tag ="79")]                  public int ScaleAboveBillion  { get; set; }
        [UI("产值规模", "规上企业(亿元以下)", Tag ="88")]                  public int ScaleBelowBillion  { get; set; }
        [UI("产值规模", "规下企业", Tag ="99")]                            public int ScaleSmall         { get; set; }
        [UI("产值规模", "小微企业", Tag ="145")]                            public int ScaleMicro         { get; set; }

        // --- 行业分类/印刷行业 ---
        [UI("行业分类/印刷行业", "胶印", Tag ="153")]                       public int IndPrintJiaoYin        { get; set; }
        [UI("行业分类/印刷行业", "凹版", Tag ="154")]                       public int IndPrintAoBan          { get; set; }
        [UI("行业分类/印刷行业", "其他印刷业", Tag ="108")]                 public int IndPrintOther          { get; set; }
        [UI("行业分类/其他行业", "印刷行业合计", Tag ="153,154,108")]               public int IndPrint             { get; set; }

        // --- 行业分类/塑料制品行业 ---
        [UI("行业分类/塑料制品行业", "塑编行业", Tag ="118")]               public int IndPlasticWoven        { get; set; }
        [UI("行业分类/塑料制品行业", "其他塑料制品行业", Tag ="119")]       public int IndPlasticOther        { get; set; }
        [UI("行业分类/其他行业", "塑料制品业合计", Tag ="118,119")]             public int IndPlastic           { get; set; }
        [UI("行业分类/其他行业", "珍珠棉", Tag ="117")]         public int FocusEpe               { get; set; }

        // --- 行业分类/其他行业 ---
        [UI("行业分类/其他行业", "纸制品业", Tag ="109")]                   public int IndPaper            { get; set; }
        [UI("行业分类/其他行业", "机械行业", Tag ="112")]                   public int IndMechanic          { get; set; }
        [UI("行业分类/其他行业", "金属制品业", Tag ="113")]                 public int IndMetal             { get; set; }
        [UI("行业分类/其他行业", "商贸行业", Tag ="114")]                   public int IndTrade             { get; set; }
        [UI("行业分类/其他行业", "其他行业", Tag ="116")]                   public int IndOther             { get; set; }

        // --- 八类危险性较高工业企业 ---
        [UI("八类危险性较高工业企业", "家电制造", Tag ="148")]              public int RiskHomeAppliance         { get; set; }
        [UI("八类危险性较高工业企业", "电镀类", Tag ="149")]                 public int RiskElectroplating    { get; set; }
        [UI("八类危险性较高工业企业", "竹木加工类", Tag ="115")]            public int RiskBamboo            { get; set; }
        [UI("八类危险性较高工业企业", "制鞋类", Tag ="150")]                 public int RiskShoe              { get; set; }
        [UI("八类危险性较高工业企业", "海绵生产类", Tag ="117")]            public int RiskSponge            { get; set; }
        [UI("八类危险性较高工业企业", "纺织印染类", Tag ="111")]            public int RiskTextile           { get; set; }
        [UI("八类危险性较高工业企业", "喷漆作业(非水性漆)", Tag ="151")]    public int RiskSprayPaint       { get; set; }
        [UI("八类危险性较高工业企业", "油墨印刷(非水性油墨)", Tag ="152")]  public int RiskInkPrint         { get; set; }

        // --- 三场所三企业 ---
        [UI("三场所三企业", "有限空间作业场所", Tag="120")]                public int ThreeLimitSpace    { get; set; }
        [UI("三场所三企业", "可燃爆粉尘作业场所", Tag="121")]              public int ThreeDust             { get; set; }
        [UI("三场所三企业", "喷涂作业场所", Tag="122")]                     public int ThreePainting         { get; set; }
        [UI("三场所三企业", "金属冶炼企业", Tag="123")]                     public int ThreeMetalSmelt       { get; set; }
        [UI("三场所三企业", "涉氨制冷企业", Tag="124")]                     public int ThreeAmmonia          { get; set; }
        [UI("三场所三企业", "船舶修造企业", Tag="125")]                     public int ThreeShipBuilding     { get; set; }

        // --- 关注风险 ---
        [UI("关注风险", "危化品使用企业", Tag="126")]                      public int FocusChemical        { get; set; }
        [UI("关注风险", "易燃企业", Tag="181")]                             public int FocusInflammable     { get; set; }
        [UI("关注风险", "有环保设备", Tag="86")]                           public int FocusEnvProtection   { get; set; }
        [UI("关注风险", "非煤矿山", Tag="172")]                             public int FocusMine            { get; set; }

        // --- 分级管控四色等级 ---
        [UI("分级管控四色等级", "白名单数")]                     public int ColorNone   { get; set; }
        [UI("分级管控四色等级", "黄名单数")]                     public int ColorLow    { get; set; }
        [UI("分级管控四色等级", "橙名单数")]                     public int ColorMedium { get; set; }
        [UI("分级管控四色等级", "红名单数")]                     public int ColorHigh   { get; set; }

        /// <summary>IExport 接口实现：供 SortPageExport/ExcelExporter 使用；
        /// 注意：此处字段名与类属性名严格一致，不做任何重命名，
        /// 以便与 Razor 视图 EleColumn For="Item.Xxx"、以及 UIAttribute.GroupPath 层级一一对应。
        /// 需要隐藏/保留的字段，由 UIAttribute.Export 控制。
        /// </summary>
        public virtual object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Index, SectionName, SectionRemark, OrgId, StartDt, EndDt,
                BaseCount, NewCount, CloseCount,
                FactoryIndependentInside, FactoryIndependentOutside,
                FactoryWorkshopInside, FactoryWorkshopOutside,
                FactoryParkInside, FactoryParkOutside,
                ScaleAboveBillion, ScaleBelowBillion, ScaleSmall, ScaleMicro,
                IndPrintJiaoYin, IndPrintAoBan, IndPrintOther,
                IndPlasticWoven, IndPlasticOther,
                IndPaper, FocusEpe, IndPrint, IndPlastic, IndMechanic, IndMetal,
                IndTrade, IndOther,
                RiskHomeAppliance, RiskElectroplating, RiskBamboo, RiskShoe,
                RiskSponge, RiskTextile, RiskSprayPaint, RiskInkPrint,
                ThreeLimitSpace, ThreeDust,
                ThreePainting, ThreeMetalSmelt, ThreeAmmonia, ThreeShipBuilding,
                FocusChemical, FocusInflammable, FocusEnvProtection, FocusMine,
                ColorNone, ColorLow, ColorMedium, ColorHigh,
            };
        }
    }

    //----------------------------------------------------------------------
    // 网格员企业分类统计行
    //----------------------------------------------------------------------
    /// <summary>网格员对象统计行（在 CheckObjectStatRow 基础上增加网格员两列）</summary>
    public class CheckObjectCheckerStatRow : CheckObjectStatRow
    {
        [UI("网格员Id",   Column = ColumnType.None)] public long?  CheckerId    { get; set; }
        [UI("网格员")]              public string CheckerName  { get; set; }

        public override object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Index,
                SectionName,
                SectionRemark,
                OrgId,
                StartDt,
                EndDt,
                CheckerId,
                CheckerName,
                BaseCount, NewCount, CloseCount,
                FactoryIndependentInside, FactoryIndependentOutside,
                FactoryWorkshopInside, FactoryWorkshopOutside,
                FactoryParkInside, FactoryParkOutside,
                ScaleAboveBillion, ScaleBelowBillion, ScaleSmall, ScaleMicro,
                IndPrintJiaoYin, IndPrintAoBan, IndPrintOther,
                IndPlasticWoven, IndPlasticOther,
                IndPaper, FocusEpe, IndPrint, IndPlastic, IndMechanic, IndMetal,
                IndTrade, IndOther,
                RiskHomeAppliance, RiskElectroplating, RiskBamboo, RiskShoe,
                RiskSponge, RiskTextile, RiskSprayPaint, RiskInkPrint,
                ThreeLimitSpace, ThreeDust,
                ThreePainting, ThreeMetalSmelt, ThreeAmmonia, ThreeShipBuilding,
                FocusChemical, FocusInflammable, FocusEnvProtection, FocusMine,
                ColorNone, ColorLow, ColorMedium, ColorHigh,
            };
        }
    }


    //----------------------------------------------------------------------
    // 企业分类统计服务
    //----------------------------------------------------------------------
    /// <summary>企业分类统计服务（对应「工矿企业分类统计表」截图）</summary>
    public static class CheckObjectStatService
    {
        sealed class TagStatField
        {
            public PropertyInfo Field;
            public long[] TagIds;
            public bool MatchAny;
        }

        // 多个标签默认要求全部匹配（如厂房类型 + 集聚区）；“合计”列则匹配任一标签。
        static readonly List<TagStatField> _tagStatFields = BuildTagStatFields();

        // 网格员报表：只统计 5 个科室（不包含应急局汇总行）
        static readonly (long OrgId, string Name, string Remark, bool IncludeSub)[] _checkerSections = new[]
        {
            ( 24L, "新城",   "(OrgId=24)",   true ),
            (129L, "城北",   "(OrgId=129)",  true ),
            (126L, "城西",   "(OrgId=126)",  true ),
            ( 73L, "城中",   "(OrgId=73)",   true ),
            (327L, "协调",   "(OrgId=327)",  true ),
        };

        static List<TagStatField> BuildTagStatFields()
        {
            return new UISetting(typeof(CheckObjectStatRow)).Items
                .Where(item => !string.IsNullOrWhiteSpace(item.Tag) && item.Field?.PropertyType == typeof(int))
                .Select(item => new TagStatField
                {
                    Field = item.Field,
                    TagIds = item.Tag.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(tag => long.TryParse(tag.Trim(), out var tagId) ? (long?)tagId : null)
                        .Where(tagId => tagId.HasValue)
                        .Select(tagId => tagId.Value)
                        .Distinct()
                        .ToArray(),
                    MatchAny = item.Title?.Contains("合计", StringComparison.Ordinal) == true,
                })
                .Where(item => item.TagIds.Length > 0)
                .ToList();
        }

        // ---------- 公共缓存入口（60 秒滑动窗口）----------
        public static List<CheckObjectStatRow> GetStat(long orgId, DateTime startDt, DateTime endDt)
        {
            var key = $"CheckObjectStat:{orgId}:{startDt:yyyyMMdd}-{endDt:yyyyMMdd}";
            var expire = DateTime.Now.AddSeconds(60);
            return Cacher.Get(key, () => ComputeCore(orgId, startDt, endDt), expire);
        }

        // ---------- 网格员 × 科室 统计入口 ----------
        public static List<CheckObjectCheckerStatRow> GetCheckerStat(DateTime startDt, DateTime endDt)
        {
            var key = $"CheckObjectCheckerStat:{startDt:yyyyMMdd}-{endDt:yyyyMMdd}";
            var expire = DateTime.Now.AddSeconds(60);
            return Cacher.Get(key, () => ComputeCheckerCore(startDt, endDt), expire);
        }

        // ---------- 统计核心（公共：把 CheckObject + TagId 全部加载 + 每行列值枚举）----------
        static List<CheckObjectStatRow> ComputeCore(long orgId, DateTime startDt, DateTime endDt)
        {
            var ctx = BuildStatContext();
            DateTime start = startDt.Date;
            DateTime end   = endDt.Date.AddDays(1).AddTicks(-1);
            bool InRange(DateTime? dt) => dt.HasValue && dt.Value >= start && dt.Value <= end;

            var orgs = App.DAL.Org.All
                .Where(org => org.ParentId == orgId)
                .OrderBy(org => org.SortId)
                .ThenBy(org => org.Id)
                .ToList();
            var currentOrg = App.DAL.Org.All.FirstOrDefault(org => org.Id == orgId);
            if (currentOrg == null)
                return new List<CheckObjectStatRow>();
            orgs.Insert(0, currentOrg);

            var rows = new List<CheckObjectStatRow>(orgs.Count);
            for (int i = 0; i < orgs.Count; i++)
            {
                var org = orgs[i];
                var orgSet = App.DAL.Org.GetChildIds(org.Id)?.ToHashSet() ?? new HashSet<long> { org.Id };

                var row = new CheckObjectStatRow
                {
                    Index         = i + 1,
                    SectionName   = org.Name,
                    SectionRemark = org.FullName,
                    OrgId         = org.Id,
                    StartDt       = startDt.Date,
                    EndDt         = endDt.Date,
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
                    ? App.DAL.Org.GetChildIds(s.OrgId)?.ToHashSet() ?? new HashSet<long> { s.OrgId }
                    : new HashSet<long> { s.OrgId };

                // 该科室下有对象的 CheckerId（distinct 升序）
                var sectionCheckerIds = new List<long>();
                foreach (var o in ctx.Objects)
                {
                    if (!o.OrgId.HasValue || !orgSet.Contains(o.OrgId.Value)) continue;
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
                        OrgId         = s.OrgId,
                        SectionName   = $"{s.Name}",
                        SectionRemark = $"根据 CheckObjectStat.OrgId= 统计",
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
            public long? OrgId; public long? CheckerId; public CheckRiskLevel? RiskLevel; public CheckObjectScale? Scale; public CheckIndustryRiskType? IndustryRisk;
        }

        static StatContext BuildStatContext()
        {
            var objTags = CheckObjectTagSpec.Set
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
                    OrgId       = o.OrgId,
                    CheckerId   = o.CheckerId,
                    RiskLevel   = o.RiskLevel,
                    Scale       = o.Scale,
                    IndustryRisk = o.IndustryRisk,
                })
                .ToList();

            return new StatContext { Objects = all, ObjTags = objTags };
        }

        static void AccumulateRow(CheckObjectStatRow row, StatContext ctx, HashSet<long> orgSet, Func<DateTime?, bool> inRange, Func<ObjLite, bool> extraWhere)
        {
            foreach (var o in ctx.Objects)
            {
                var inThis = o.OrgId.HasValue && orgSet.Contains(o.OrgId.Value);
                if (!inThis) continue;
                if (!extraWhere(o)) continue;

                // 数量及增减
                if (!o.IsDel.GetValueOrDefault()) row.BaseCount++;
                if (inRange(o.CreateDt))          row.NewCount++;
                if (inRange(o.DeleteDt))          row.CloseCount++;

                // 标签统计
                var oid = o.Id;
                if (ctx.ObjTags.TryGetValue(oid, out var objectTags))
                {
                    foreach (var statField in _tagStatFields)
                    {
                        bool matched = statField.MatchAny
                            ? statField.TagIds.Any(objectTags.Contains)
                            : statField.TagIds.All(objectTags.Contains);
                        if (!matched) continue;

                        var count = (int)statField.Field.GetValue(row);
                        statField.Field.SetValue(row, count + 1);
                    }
                }

                // 风险等级四色
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
