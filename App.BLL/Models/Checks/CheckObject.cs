using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;
using Z.EntityFramework.Plus;
using App.Components;
using Microsoft.EntityFrameworkCore;


/*
检查对象CheckObject --(1:n)-- 检查对象联系人CheckObjectContact
                  --(1:n)-- 检查对象标签CheckTag
*/
namespace App.DAL
{

    /// <summary>检查对象</summary>
    [UI("检查", "检查对象")]
    public class CheckObject : EntityBase<CheckObject>, IDeleteLogic
    {
        [UI("基础", "是否有效")]    public bool? InUsed { get; set; } = true;
        [UI("基础", "名称")]        public string Name { get; set;}
        [UI("基础", "编码")]        public string Code { get; set;}  // XC（中心编号）-01（网格员编号）-001（网格企业编号）
        [UI("基础", "地块号")]       public string AreaCode { get; set; }
        [UI("基础", "社会信用代码")]  public string SocialCreditCode { get; set;}
        [UI("基础", "地址")]        public string Address { get; set; }
        [UI("基础", "GPS")]        public string Gps { get; set; }
        [UI("基础", "建档日期")]     public DateTime? ArchieveDt { get; set; }
        [UI("基础", "领域")]        public string Field { get; set; }  // 需枚举

        [UI("基础", "风险等级")]    public CheckRiskLevel? RiskLevel { get; set; }
        [UI("基础", "风险四色")]    public CheckRiskColor? RiskColor { get; set; }
        [UI("基础", "领域")]       public CheckScope? Scope { get; set; }  
        [UI("基础", "规模")]       public CheckObjectScale? Scale { get; set; }
        [UI("基础", "类型")]       public CheckObjectType? ObjectType { get; set; }

        [UI("基础", "责任组织")]    public long? DutyOrgId { get; set; }
        [UI("基础", "技术检查员")] public long? CheckerId { get; set; }
        [UI("基础", "社区网格员")] public long? SocialCheckerId { get; set; }
        [NotMapped] public string CheckerName { get; set; }
        [NotMapped] public string SocialCheckerName { get; set; }
        [UI("基础", "电表号")] public string EleMeeterNum { get; set; }
        [UI("基础", "员工数")] public int? EmployeeCount { get; set; }

        [UI("基础", "生产内容")] public string ProductContent { get; set; }
        [UI("基础", "外观图片")] public string OutlookImage { get; set; }
        [UI("基础", "工商执照")] public string LicenseImage { get; set; }

        // 数据相关
        [UI("数据", "是否录入平台")] public bool? IsInOnlinePlatform { get; set; }
        [UI("数据", "是否录入141平台")] public bool? IsIn141Platform { get; set; }


        // 风险相关
        [UI("风险", "行业类型")] public CheckIndustryType? IndustryType { get; set; }  // 需枚举
        [UI("风险", "行业风险")] public CheckIndustryRiskType? IndustryRisk { get; set; }  // 需枚举
        [UI("风险", "亿元企业")] public bool? IsYiEnterprise { get; set; }
        [UI("风险", "重点监管")] public bool? IsKeySupervision { get; set; }
        [UI("风险", "示范企业")] public bool? IsDemonstration { get; set; }
        [UI("风险", "夜间生产")] public bool? IsProductInNight { get; set; }
        [UI("风险", "春节生产")] public bool? IsProductInSpringFestival { get; set; }
        [UI("风险", "三场所三企业")] public bool? IsThreePlacesThreeEnterprises { get; set; }
        [UI("风险", "园中园厂中厂")] public bool? IsParkFactoryOverlayRisk { get; set; }
        [UI("风险", "涉及电气焊")] public bool? HasWelding { get; set; }
        [UI("风险", "环保设备")] public bool? HasEnvironmentalEquipment { get; set; }
        [UI("风险", "喷淋系统")] public bool HasSprinklerSystem { get; set; }

        // 人员相关
        [UI("人员", "三方机构")] public string ThirdPartySafetyAgency { get; set; }
        [UI("人员", "负责人信息")] public string DutyUserName { get; set; }
        [UI("人员", "安全管理员")] public string SafetyAdminName { get; set; }
        [UI("人员", "安全管家")] public string SafetySteward { get; set; }
        [UI("人员", "内部奖励机制")] public string InternalRewardMechanism { get; set; }
        [UI("人员", "标准化创建")] public string StandardizationStatus { get; set; }

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
        public virtual User SocialChecker { get; set; }


        // 导出
        public override object Export(ExportMode mode)
        {
            return new
            {
                Id,
                Name,
                Code,
                SocialCreditCode,
                Address,
                Gps,
                AreaCode,
                ArchieveDt,
                Field,
                RiskLevel,
                RiskColor,
                Scope,
                Scale,
                ObjectType,
                DutyOrgId,
                CheckerId,
                CheckerName = Checker?.Name,
                SocialCheckerId,
                SocialCheckerName = SocialChecker?.Name,
                EleMeeterNum,
                EmployeeCount,
                ProductContent,
                OutlookImage,
                LicenseImage,
                IsInOnlinePlatform,
                IsIn141Platform,
                IsKeySupervision,
                IsDemonstration,
                IsProductInNight,
                IsProductInSpringFestival,
                IsThreePlacesThreeEnterprises,
                IsParkFactoryOverlayRisk,
                HasWelding,
                HasEnvironmentalEquipment,
                ThirdPartySafetyAgency,
                DutyUserName,
                SafetyAdminName,
                SafetySteward,
                InternalRewardMechanism,
                StandardizationStatus,
                BuildingType,
                LandArea,
                BuildingArea,
                FactoryUsageType,
                BuildingStructure,
                HasSprinklerSystem,
                IndustryType,
                IndustryRisk,
            };
        }

        public static IQueryable<CheckObject> Search(string name, string socialCreditCode, long? orgId, long? checkerId, CheckObjectType? objectType, CheckObjectScale? scale)
        {
            IQueryable<CheckObject>            q = CheckObject.IncludeSet;
            if (orgId.IsNotEmpty())            q = q.Where(o => o.DutyOrgId == orgId.Value);
            if (checkerId.IsNotEmpty())        q = q.Where(o => o.CheckerId == checkerId.Value);
            if (name.IsNotEmpty())             q = q.Where(o => o.Name.Contains(name.Trim()));
            if (objectType.IsNotEmpty())       q = q.Where(o => o.ObjectType == objectType.Value);
            if (scale.IsNotEmpty())            q = q.Where(o => o.Scale == scale.Value);
            if (socialCreditCode.IsNotEmpty()) q = q.Where(o => o.SocialCreditCode.Contains(socialCreditCode.Trim()));

            return q;
        }
    }

}