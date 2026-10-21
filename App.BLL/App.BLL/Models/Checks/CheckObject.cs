using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using App.DAL.GIS;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;
using Z.EntityFramework.Plus;

/*
检查对象CheckObject --(1:n)-- 检查对象联系人CheckObjectContact
                  --(1:n)-- 检查对象标签CheckTag
*/
namespace App.DAL
{
    /// <summary>检查对象</summary>
    [UI("检查", "检查对象")]
    public class CheckObject : EntityBase<CheckObject>, IDeleteLogic, IFixAll
    {
        //  IDeleteLogic 实现
        [UI("基础", "是否失效")] public bool? IsDel { get; set; } = false;
        [UI("基础", "失效时间")] public DateTime? DeleteDt { get; set; }

        //
        [UI("基础", "编码")]      public string Code { get; set; }
        [UI("基础", "失效原因")]    public CheckObjectFailReason? FailReason { get; set; }
        [UI("基础", "是否存在隐患")] public bool? HasHarzard { get; set; } = false;
        [UI("基础", "名称")]        public string Name { get; set;}
        [UI("基础", "地块号")]       public string AreaCode { get; set; }
        [UI("基础", "社会信用代码")]  public string SocialCreditId { get; set;}
        [UI("基础", "地址")]        public string Address { get; set; }
        [UI("基础", "GPS")]        public string Gps { get; set; }
        [UI("基础", "是否巡查")]     public bool? IsChecked { get; set; } 
        [UI("基础", "建档日期")]     public DateTime? ArchieveDt { get; set; }
        [UI("基础", "最新巡查时间")] public DateTime? LastCheckDt { get; set; }
        [UI("基础", "规模")]        public CheckObjectScale? Scale { get; set; }
        [UI("基础", "下次巡查时间")] public DateTime? NextCheckDt {get; set;}


        // 风险相关
        [UI("基础", "风险等级")]    public CheckRiskLevel? RiskLevel { get; set; }     // 141 风险等级
        [UI("基础", "领域")]       public CheckScope? Scope { get; set; }   // 141 领域
        [UI("基础", "类型")]       public CheckObjectType? ObjectType { get; set; }   // 141 类型
        [UI("风险", "行业类型")]    public CheckIndustryType? IndustryType { get; set; }  // 工业类型
        [UI("风险", "行业风险")]    public CheckIndustryRiskType? IndustryRisk { get; set; }  // 工业风险


        //
        [UI("基础", "责任网格")]    public long? OrgId { get; set; }
        [UI("基础", "网格员")]      public long? CheckerId { get; set; }

        //
        [UI("基础", "电表号")] public string EleMeeterNum { get; set; }
        [UI("基础", "员工人数")] public int? EmployeeCount { get; set; }

        [UI("基础", "生产内容")] public string ProductContent { get; set; }
        [UI("基础", "外观图片")] public string OutlookImage { get; set; }
        [UI("基础", "工商执照")] public string LicenseImage { get; set; }
        [UI("基础", "扩展信息")] public string Remark { get; set; }

        // 人员相关
        [UI("人员", "企业负责人")] public string DutyMan { get; set; }   // 格式如：张三/15300000000。Man 是 Manager 的简写
        [UI("人员", "企业安全管理员")] public string SafeMan { get; set; }  // 格式如：张三/15300000000
        [UI("人员", "企业安全管家")] public string SafeSteward { get; set; }  // 格式如：张三/15300000000
        [UI("人员", "三方安全机构")] public string SafeAgency { get; set; }  // 格式如：张三/15300000000

        // 建筑相关
        [UI("建筑", "建筑类型")] public CheckBuildingType? BuildingType { get; set; }
        [UI("建筑", "占地面积")] public double? LandArea { get; set; }
        [UI("建筑", "建筑面积")] public double? BuildingArea { get; set; }
        [UI("建筑", "厂房使用权")] public CheckFactoryUsageType? FactoryUsageType { get; set; }
        [UI("建筑", "房屋结构")] public CheckBuildingStructure? BuildingStructure { get; set; }


        // Relations
        public virtual List<CheckObjectTag> Tags { get; set; }  // 标签列表（1:n 关系）
        public virtual List<CheckObjectContact> Contacts { get; set; }  // 联系人列表（1:n 关系）
        public virtual Org Org { get; set; }
        public virtual User Checker { get; set; }

        // 扩展字段，供列表展示
        [UI("基础", "责任网格")]   public string OrgName => Org?.FullName ?? Org?.Name;
        [UI("基础", "责任单位")]   public string OrgUnitName => this.Org?.GetAncestor(OrgLevel.Unit)?.Name;         // 责任单位
        [UI("基础", "责任科室")]   public string OrgDeptName => this.Org?.GetAncestor(OrgLevel.Dept)?.Name;         // 责任科室
        [UI("基础", "责任区县")]   public string OrgDistrictName => this.Org?.GetAncestor(OrgLevel.District)?.Name;         // 责任区县
        [UI("基础", "责任乡镇")]   public string OrgTownName => this.Org?.GetAncestor(OrgLevel.Town)?.Name;         // 责任乡镇
        [UI("基础", "责任社区")]   public string OrgCommunityName => this.Org?.GetAncestor(OrgLevel.Community)?.Name;         // 责任社区
        [UI("基础", "技术检查员")] public string CheckerName => $"{Checker?.RealName}({Checker?.Mobile})";
        [UI("基础", "检查周期")]   public string CheckCycle => GetCheckCycleMonths(RiskLevel) + "个月";


        // Tag 相关信息
        private List<long> _tagIds;

        [UI("基础", "标签")]      public List<string> TagNames => Tags?.Select(t => t.Tag?.Name).ToList() ?? new List<string>();
        [NotMapped, UI("基础", "标签")]
        public List<long> TagIds
        {
            get
            {
                if (_tagIds != null)
                    return _tagIds;
                if (Tags != null && Tags.Count > 0)
                    return Tags.Select(t => t.TagId).ToList();
                return new List<long>();
            }
            set => _tagIds = value ?? new List<long>();
        }


        /// <summary>设置匹配的标签列表</summary>
        /// <param name="tagIds">标签ID列表</param>
        public void SetTags(List<long> tagIds)
        {
            var ids = (tagIds ?? new List<long>()).Distinct().Where(x => x > 0).ToList();
            CheckObjectTag.Set.Where(t => t.CheckObjectId == Id).Delete();
            foreach (var tagId in ids)
            {
                var tag = CheckTag.Set.Find(tagId);
                if (tag != null)
                    new CheckObjectTag { TagId = tag.Id, CheckObjectId = Id }.Save();
            }
        }


        // 导出
        public override object Export(ExportMode mode)
        {
            var checker = this.Checker;
            return new
            {
                Id,
                IsDel,
                FailReason,
                HasHarzard,
                Name,
                Code,
                SocialCreditId,
                Address,
                Gps,
                AreaCode,
                IsChecked,
                Remark,

                ArchieveDt,
                LastCheckDt,
                NextCheckDt,
                CreateDt,


                RiskLevel,
                CheckCycle,
                Scope,
                Scale,
                ObjectType,

                // 责任网格相关字段
                OrgId,
                OrgName,        // 责任网格
                OrgDistrictName,   // 责任区县
                OrgTownName,       // 责任乡镇
                OrgCommunityName,  // 责任社区
                OrgUnitName,       // 责任单位
                OrgDeptName,       // 责任科室
                
                CheckerId,
                CheckerName,
                EleMeeterNum,
                EmployeeCount,
                ProductContent,
                OutlookImage,
                LicenseImage,
                SafeAgency,
                DutyMan     = mode==ExportMode.Detail ? DutyMan     : DutyMan.Mask(),
                SafeMan     = mode==ExportMode.Detail ? SafeMan     : SafeMan.Mask(),
                SafeSteward = mode==ExportMode.Detail ? SafeSteward : SafeSteward.Mask(),
                BuildingType,
                LandArea,
                BuildingArea,
                FactoryUsageType,
                BuildingStructure,
                IndustryType,
                IndustryRisk,
                TagIds,
                TagNames,
                Tags = Tags?.Select(t => t.Export(mode)).ToList(),
            };
        }

        /// <summary>导出统一 GIS 点位数据</summary>
        public GeometryItem ToGeometryItem()
        {
            return new GeometryItem
            {
                Id = Id,
                RawId = Id,
                Type = GeometryType.Point,
                MenuId = null,
                SortId = 0,
                Name = Name,
                Alias = null,
                Addr = Address,
                Gps = Gps,
                Region = null,
                Url = null,
                File = null,
                GeoJson = null,
                Remark = this.Remark,
                DataJson = JsonSerializer.Serialize(new
                {
                    SocialCreditId,
                    RiskLevel,
                    Scope,
                    Scale,
                    ObjectType,
                    OrgId,
                    OrgName,
                    CheckerId,
                    CheckerName,
                    DutyMan,
                    TagNames,
                    IsDel
                }),
                IsVisible = true,
                Icon = null,
                MenuName = null,
                DataFrom = GisDataFrom.API
            };
        }

        /// <summary>获取详情（包含 Tags/Contacts 等集合导航属性）</summary>
        public new static CheckObject GetDetail(long id)
        {
            return IncludeSet
                .Include(o => o.Tags).ThenInclude(t => t.Tag)
                .Include(o => o.Contacts)
                .FirstOrDefault(o => o.Id == id);
        }

        public static IQueryable<CheckObject> Search(
            string name = "",
            string code = "",
            string socialCreditCode = "",
            string address = "",
            string dutyMan = "",      // 企业责任人
            long? orgId = null,
            List<long> orgIds = null,
            List<long> tagIds = null,
            long? checkerId = null,   // 网格员
            CheckObjectType? objectType = null,
            CheckScope? scope = null,
            CheckObjectScale? scale = null,
            CheckRiskLevel? riskLevel = null,
            CheckIndustryType? industryType = null,
            DateTime? createStartDt = null,
            DateTime? createEndDt = null,
            DateTime? updateStartDt = null,
            DateTime? updateEndDt = null,
            DateTime? lastCheckStartDt = null,
            DateTime? lastCheckEndDt = null,
            DateTime? nextCheckFrom = null,
            DateTime? nextCheckTo = null,
            bool? hasHarzard = null,
            bool? isChecked = null,
            bool? isDel = null,
            bool? isProductInNight = null,
            bool? isThreePlacesThreeEnterprises = null,
            bool includeTags = false,     // 是否包含标签导航属性
            bool includeContacts = false  // 是否包含联系人导航属性
        )
        {
            IQueryable<CheckObject> q = CheckObject.Set
                .Include(o => o.Org)
                .Include(o => o.Checker)
                ;
            orgIds = GetOrgIds(orgIds, orgId);
            if (includeContacts)                      q = q.Include(o => o.Contacts);
            if (orgIds.Count > 0)                     q = q.Where(o => o.OrgId.HasValue && orgIds.Contains(o.OrgId.Value));
            if (checkerId.IsNotEmpty())               q = q.Where(o => o.CheckerId == checkerId.Value);
            if (name.IsNotEmpty())                    q = q.Where(o => o.Name.Contains(name.Trim()));
            if (code.IsNotEmpty())                    q = q.Where(o => o.Code.Contains(code.Trim()));
            if (address.IsNotEmpty())                 q = q.Where(o => o.Address.Contains(address.Trim()));
            if (dutyMan.IsNotEmpty())                 q = q.Where(o => o.DutyMan.Contains(dutyMan.Trim()));
            // 注意：GetTagIds 已把每个用户勾选标签展开为"该标签+它所有子孙标签"；
            // AND 语义：最终结果对象，对于每个用户勾选的标签，都要"命中该标签或其任一子孙标签"。
            // 所以用用户勾选的原始 tagIds 做外层 AND 循环，每一项在已展开的子集合里做 OR。
            var tagIdGroups = GetTagIdGroups(tagIds);
            if (includeTags) q = q.Include(o => o.Tags).ThenInclude(t => t.Tag);
            // AND 过滤：用户勾选的每一个 tag，对象都必须"包含该 tag 或包含其任意子孙 tag"
            foreach (var grp in tagIdGroups)
            {
                if (grp.Count == 0) continue;
                var group = grp;
                q = q.Where(o => CheckObjectTag.IncludeSet.Any(t => t.CheckObjectId == o.Id && group.Contains(t.TagId)));
            }
            if (objectType.IsNotEmpty())              q = q.Where(o => o.ObjectType == objectType.Value);
            if (scope.IsNotEmpty())                   q = q.Where(o => o.Scope == scope.Value);
            if (scale.IsNotEmpty())                   q = q.Where(o => o.Scale == scale.Value);
            if (riskLevel.IsNotEmpty())               q = q.Where(o => o.RiskLevel == riskLevel.Value);
            if (industryType.IsNotEmpty())            q = q.Where(o => o.IndustryType == industryType.Value);
            if (socialCreditCode.IsNotEmpty())        q = q.Where(o => o.SocialCreditId.Contains(socialCreditCode.Trim()));
            if (createStartDt.IsNotEmpty())           q = q.Where(o => o.CreateDt >= createStartDt.Value);
            if (createEndDt.IsNotEmpty())             q = q.Where(o => o.CreateDt <= createEndDt.Value);
            if (updateStartDt.IsNotEmpty())           q = q.Where(o => o.UpdateDt >= updateStartDt.Value);
            if (updateEndDt.IsNotEmpty())             q = q.Where(o => o.UpdateDt <= updateEndDt.Value);
            if (lastCheckStartDt.IsNotEmpty())        q = q.Where(o => o.LastCheckDt >= lastCheckStartDt.Value);
            if (lastCheckEndDt.IsNotEmpty())          q = q.Where(o => o.LastCheckDt <= lastCheckEndDt.Value);
            if (nextCheckFrom.IsNotEmpty())           q = q.Where(o => o.NextCheckDt >= nextCheckFrom.Value);
            if (nextCheckTo.IsNotEmpty())             q = q.Where(o => o.NextCheckDt <= nextCheckTo.Value);
            if (hasHarzard.IsNotEmpty())              q = q.Where(o => o.HasHarzard == hasHarzard.Value);
            if (isChecked.IsNotEmpty())               q = q.Where(o => o.IsChecked == isChecked.Value);
            if (isDel.IsNotEmpty())                   q = q.Where(o => o.IsDel == isDel.Value);

            return q;
        }

        /// <summary>递归获取网格及其子网格Id。先取 orgIds 列表，为空再回退 orgId 单个值。</summary>
        private static List<long> GetOrgIds(List<long> orgIds, long? orgId)
        {
            // 合并两个来源 + 去重
            var list = new List<long>();
            if (orgIds != null && orgIds.Count > 0) list.AddRange(orgIds);
            if (orgId.IsNotEmpty()) list.Add(orgId.Value);
            list = list.Distinct().ToList();
            if (list.Count == 0)
                return new List<long>();

            return Org.All
                .GetDescendants(list)
                .Select(t => t.Id)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// 根据用户勾选的标签列表，返回“每个勾选标签一组”的 ID 集合（每组 = 该标签 + 所有子孙标签）。
        /// Search 中将把每一组做 AND：最终对象必须“命中每一组中至少一个 tagId”。
        /// </summary>
        private static List<List<long>> GetTagIdGroups(List<long> tagIds)
        {
            var list = new List<List<long>>();
            if (tagIds == null || tagIds.Count == 0) return list;

            var distinct = tagIds.Distinct().ToList();
            foreach (var tid in distinct)
            {
                // 叶子节点（无子孙）也要保留自身作为单元素组；非叶子展开自身+子孙
                var group = CheckTag.All
                    .GetDescendants(tid)
                    .Select(t => t.Id)
                    .DefaultIfEmpty(tid)
                    .Distinct()
                    .ToList();
                if (group.Count == 0) group = new List<long> { tid };
                list.Add(group);
            }
            return list;
        }

        /// <summary>（兼容旧调用）递归获取标签及其子标签Id。返回所有输入标签+子孙的扁平化合集。</summary>
        private static List<long> GetTagIds(List<long> tagIds)
        {
            var flat = new List<long>();
            foreach (var grp in GetTagIdGroups(tagIds)) flat.AddRange(grp);
            return flat.Distinct().ToList();
        }

        // 根据风险等级获取检查周期（月数）
        static int GetCheckCycleMonths(CheckRiskLevel? riskLevel)
        {
            return riskLevel switch
            {
                CheckRiskLevel.None => 12,
                CheckRiskLevel.Low => 9,
                CheckRiskLevel.Medium => 6,
                CheckRiskLevel.High => 3,
                _ => 12
            };
        }        

        /// <summary>批量修复所有检查对象（两条 UPDATE 语句：补 NextCheckDt、重算 IsChecked）。</summary>
        /// <returns>受影响的行数</returns>
        public static int FixAll()
        {
            var now = DateTime.Now;

            // 1) 补全“下次巡查时间”：NextCheckDt = LastCheckDt + 检查周期（月数映射同 GetCheckCycleMonths），仅处理缺失的记录
            CheckObject.Set
                .Where(o => o.RiskLevel != null && o.LastCheckDt != null && o.NextCheckDt == null)
                .ExecuteUpdate(s => s
                    .SetProperty(o => o.NextCheckDt, o => o.LastCheckDt.Value.AddMonths(
                        o.RiskLevel == CheckRiskLevel.High ? 3 :
                        o.RiskLevel == CheckRiskLevel.Medium ? 6 :
                        o.RiskLevel == CheckRiskLevel.Low ? 9 : 12))
                    .SetProperty(o => o.UpdateDt, now));

            // 2) 重算“是否巡查”状态：IsChecked = 下次巡查时间是否尚未过期
            CheckObject.Set
                .Where(o => o.IsChecked == null || o.IsChecked != (o.NextCheckDt != null && o.NextCheckDt > now))
                .ExecuteUpdate(s => s
                    .SetProperty(o => o.IsChecked, o => o.NextCheckDt != null && o.NextCheckDt > now)
                    .SetProperty(o => o.UpdateDt, now));

            CheckObject.ClearCache();

            // 两条 UPDATE 都会被 UpdateDt 记为本次时间；据此统计实际修改的记录数（避免两条语句重复计数）
            return CheckObject.Set.Count(o => o.UpdateDt == now);
        }


    }

}
