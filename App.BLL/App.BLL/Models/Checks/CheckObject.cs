using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
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
        [UI("基础", "编码")]      public string Code { get; set; }
        [UI("基础", "是否失效")]    public bool? IsDel { get; set; } = false;
        [UI("基础", "失效原因")]    public string FailReason { get; set; }
        [UI("基础", "是否存在隐患")] public bool? HasHarzard { get; set; } = false;
        [UI("基础", "名称")]        public string Name { get; set;}
        [UI("基础", "地块号")]       public string AreaCode { get; set; }
        [UI("基础", "社会信用代码")]  public string SocialCreditCode { get; set;}
        [UI("基础", "地址")]        public string Address { get; set; }
        [UI("基础", "GPS")]        public string Gps { get; set; }
        [UI("基础", "是否巡查")]     public bool? IsChecked { get; set; } 
        [UI("基础", "建档日期")]     public DateTime? ArchieveDt { get; set; }
        [UI("基础", "最新巡查时间")] public DateTime? LatestCheckDt { get; set; }
        [UI("基础", "领域")]        public string Field { get; set; }  // 需枚举

        [UI("基础", "风险等级")]    public CheckRiskLevel? RiskLevel { get; set; }
        [UI("基础", "领域")]       public CheckScope? Scope { get; set; }  
        [UI("基础", "规模")]       public CheckObjectScale? Scale { get; set; }
        [UI("基础", "类型")]       public CheckObjectType? ObjectType { get; set; }

        [UI("基础", "责任网格")]    public long? DutyOrgId { get; set; }


        [UI("基础", "检查员")] public long? CheckerId { get; set; }
        [UI("基础", "电表号")] public string EleMeeterNum { get; set; }
        [UI("基础", "员工人数")] public int? EmployeeCount { get; set; }

        [UI("基础", "生产内容")] public string ProductContent { get; set; }
        [UI("基础", "外观图片")] public string OutlookImage { get; set; }
        [UI("基础", "工商执照")] public string LicenseImage { get; set; }

        // 数据相关
        [UI("数据", "是否录入平台")] public bool? IsInOnlinePlatform { get; set; }
        [UI("数据", "是否录入141平台")] public bool? IsIn141Platform { get; set; }


        // 风险相关
        [UI("风险", "行业类型")] public CheckIndustryType? IndustryType { get; set; }  // 枚举（考虑用tag实现，更容易扩展维护）
        [UI("风险", "行业风险")] public CheckIndustryRiskType? IndustryRisk { get; set; }  // 枚举（考虑用tag实现，更容易扩展维护）

        //
        [UI("其它", "重点监管")] public bool? IsKeySupervision { get; set; }
        [UI("其它", "示范企业")] public bool? IsDemonstration { get; set; } 
        [UI("其它", "内部奖励机制")] public string InternalRewardMechanism { get; set; }

        // 人员相关
        [UI("人员", "三方机构")] public string ThirdPartySafetyAgency { get; set; }
        [UI("人员", "负责人信息")] public string DutyUserName { get; set; }
        [UI("人员", "安全管理员")] public string SafetyAdminName { get; set; }
        [UI("人员", "安全管家")] public string SafetySteward { get; set; }

        // 建筑相关
        [UI("建筑", "建筑类型")] public CheckBuildingType? BuildingType { get; set; }
        [UI("建筑", "占地面积")] public double? LandArea { get; set; }
        [UI("建筑", "建筑面积")] public double? BuildingArea { get; set; }
        [UI("建筑", "厂房使用权")] public CheckFactoryUsageType? FactoryUsageType { get; set; }  // 需枚举
        [UI("建筑", "房屋结构")] public CheckBuildingStructure? BuildingStructure { get; set; }   // 需枚举


        // Relations
        public virtual List<CheckObjectTag> Tags { get; set; }  // 标签列表（1:n 关系）
        public virtual List<CheckObjectContact> Contacts { get; set; }  // 联系人列表（1:n 关系）
        public virtual Org DutyOrg { get; set; }
        public virtual User Checker { get; set; }

        // 扩展字段，供列表展示
        [UI("基础", "责任网格")]   public string DutyOrgName => DutyOrg?.FullName ?? DutyOrg?.Name;
        [UI("基础", "责任单位")]   public string DutyUnitName => this.DutyOrg?.GetAncestor(OrgLevel.Unit)?.Name;         // 责任单位
        [UI("基础", "责任区县")]   public string DutyDistrictName => this.DutyOrg?.GetAncestor(OrgLevel.District)?.Name;         // 责任区县
        [UI("基础", "责任乡镇")]   public string DutyTownName => this.DutyOrg?.GetAncestor(OrgLevel.Town)?.Name;         // 责任乡镇
        [UI("基础", "责任社区")]   public string DutyCommunityName => this.DutyOrg?.GetAncestor(OrgLevel.Community)?.Name;         // 责任社区

        [UI("基础", "技术检查员")] public string CheckerName => Checker?.Name;
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
            return new
            {
                Id,
                IsDel,
                FailReason,
                HasHarzard,
                Name,
                Code,
                SocialCreditCode,
                Address,
                Gps,
                AreaCode,
                IsChecked,
                ArchieveDt,
                LatestCheckDt,
                Field,
                RiskLevel,
                CheckCycle,
                Scope,
                Scale,
                ObjectType,

                // 责任网格相关字段
                DutyOrgId,
                DutyOrgName,        // 责任网格
                DutyDistrictName,   // 责任区县
                DutyTownName,       // 责任乡镇
                DutyCommunityName,  // 责任社区
                DutyUnitName,       // 责任单位
                
                CheckerId,
                CheckerName,
                EleMeeterNum,
                EmployeeCount,
                ProductContent,
                OutlookImage,
                LicenseImage,
                IsInOnlinePlatform,
                IsIn141Platform,
                IsKeySupervision,
                IsDemonstration,
                ThirdPartySafetyAgency,
                DutyUserName,
                SafetyAdminName,
                SafetySteward,
                InternalRewardMechanism,
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
            string dutyUserName = "",
            long? dutyOrgId = null,
            List<long> tagIds = null,
            long? checkerId = null,
            CheckObjectType? objectType = null,
            CheckScope? scope = null,
            CheckObjectScale? scale = null,
            CheckRiskLevel? riskLevel = null,
            CheckIndustryType? industryType = null,
            DateTime? createStartDt = null,
            DateTime? createEndDt = null,
            DateTime? updateStartDt = null,
            DateTime? updateEndDt = null,
            DateTime? latestCheckStartDt = null,
            DateTime? latestCheckEndDt = null,
            bool? hasHarzard = null,
            bool? isChecked = null,
            bool? isDel = null,
            bool? isDemonstration = null,
            bool? isKeySupervision = null,
            bool? isProductInNight = null,
            bool? isThreePlacesThreeEnterprises = null,
            bool includeTags = false,
            bool includeContacts = false
        )
        {
            IQueryable<CheckObject> q = CheckObject.Set
                .Include(o => o.DutyOrg)
                .Include(o => o.Checker)
                ;
            var dutyNetOrgIds = GetOrgIds(dutyOrgId);
            var allTagIds = GetTagIds(tagIds);
            if (includeTags)                          q = q.Include(o => o.Tags).ThenInclude(t => t.Tag);
            if (includeContacts)                      q = q.Include(o => o.Contacts);
            if (dutyNetOrgIds.Count > 0)              q = q.Where(o => o.DutyOrgId.HasValue && dutyNetOrgIds.Contains(o.DutyOrgId.Value));
            if (checkerId.IsNotEmpty())               q = q.Where(o => o.CheckerId == checkerId.Value);
            if (name.IsNotEmpty())                    q = q.Where(o => o.Name.Contains(name.Trim()));
            if (code.IsNotEmpty())                    q = q.Where(o => o.Code.Contains(code.Trim()));
            if (address.IsNotEmpty())                 q = q.Where(o => o.Address.Contains(address.Trim()));
            if (dutyUserName.IsNotEmpty())            q = q.Where(o => o.DutyUserName.Contains(dutyUserName.Trim()));
            if (allTagIds.Count > 0)                  q = q.Where(o => CheckObjectTag.IncludeSet.Any(t => t.CheckObjectId == o.Id && allTagIds.Contains(t.TagId)));
            if (objectType.IsNotEmpty())              q = q.Where(o => o.ObjectType == objectType.Value);
            if (scope.IsNotEmpty())                   q = q.Where(o => o.Scope == scope.Value);
            if (scale.IsNotEmpty())                   q = q.Where(o => o.Scale == scale.Value);
            if (riskLevel.IsNotEmpty())               q = q.Where(o => o.RiskLevel == riskLevel.Value);
            if (industryType.IsNotEmpty())            q = q.Where(o => o.IndustryType == industryType.Value);
            if (socialCreditCode.IsNotEmpty())        q = q.Where(o => o.SocialCreditCode.Contains(socialCreditCode.Trim()));
            if (createStartDt.IsNotEmpty())           q = q.Where(o => o.CreateDt >= createStartDt.Value);
            if (createEndDt.IsNotEmpty())             q = q.Where(o => o.CreateDt <= createEndDt.Value);
            if (updateStartDt.IsNotEmpty())           q = q.Where(o => o.UpdateDt >= updateStartDt.Value);
            if (updateEndDt.IsNotEmpty())             q = q.Where(o => o.UpdateDt <= updateEndDt.Value);
            if (latestCheckStartDt.IsNotEmpty())      q = q.Where(o => o.LatestCheckDt >= latestCheckStartDt.Value);
            if (latestCheckEndDt.IsNotEmpty())        q = q.Where(o => o.LatestCheckDt <= latestCheckEndDt.Value);
            if (hasHarzard.IsNotEmpty())              q = q.Where(o => o.HasHarzard == hasHarzard.Value);
            if (isChecked.IsNotEmpty())               q = q.Where(o => o.IsChecked == isChecked.Value);
            if (isDel.IsNotEmpty())                   q = q.Where(o => o.IsDel == isDel.Value);
            if (isDemonstration.IsNotEmpty())         q = q.Where(o => o.IsDemonstration == isDemonstration.Value);
            if (isKeySupervision.IsNotEmpty())        q = q.Where(o => o.IsKeySupervision == isKeySupervision.Value);

            return q;
        }

        /// <summary>递归获取网格及其子网格Id。</summary>
        private static List<long> GetOrgIds(long? orgId)
        {
            if (!orgId.IsNotEmpty())
                return new List<long>();

            return Org.All
                .GetDescendants(orgId.Value)
                .Select(t => t.Id)
                .Distinct()
                .ToList();
        }

        /// <summary>递归获取标签及其子标签Id。</summary>
        private static List<long> GetTagIds(List<long> tagIds)
        {
            if (tagIds == null || tagIds.Count == 0)
                return new List<long>();

            return CheckTag.All
                .GetDescendants(tagIds.Distinct().ToList())
                .Select(t => t.Id)
                .Distinct()
                .ToList();
        }

        /// <summary>修复所有检查对象的IsChecked字段</summary>
        /// <returns>修复的检查对象数量</returns>
        public static int FixAll()
        {
            // 根据最后更新时间，以及对象的风险等级 RiskLevel（对应到检查周期CheckCycle），更新IsChecked字段
            var cnt = 0;
            var now = DateTime.Now;
            var list = CheckObject.IncludeSet.ToList();
            foreach (var item in list)
            {
                var isChecked = false; // 计算得到的新值
                if (item.RiskLevel.HasValue && item.LatestCheckDt.HasValue)
                {
                    var checkCycleMonths = GetCheckCycleMonths(item.RiskLevel);
                    isChecked = item.LatestCheckDt.Value.AddMonths(checkCycleMonths) > now; // 已到检查周期需要检查了（检查状态为false）
                }
                else
                {
                    isChecked = false; // 无风险等级或无巡查时间的对象，设置为false（尚未检查）
                }

                // 如果计算得到的新值与原值不同，则更新并计数
                if (item.IsChecked != isChecked)
                {
                    item.IsChecked = isChecked;
                    cnt++;
                }
            }
            return cnt;
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
    }

}
