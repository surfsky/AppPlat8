using System;
using System.Linq;
using App.Entities;
using App.Utils;
using Z.EntityFramework.Plus;


/*
检查对象CheckObject --(1:n)-- 检查点 CheckObjectPoint
*/
namespace App.DAL
{
    /// <summary>检查点，可存储风险点，也可存储巡检地点</summary>
    [UI("检查", "检查点")]
    public class CheckPoint : EntityBase<CheckPoint>
    {
        [UI("检查对象")] public long? CheckObjectId { get; set; }
        [UI("风险等级")] public CheckRiskLevel? RiskLevel { get; set; }
        [UI("名称")] public string Name { get; set; }
        [UI("图片")] public string Picture { get; set; }
        [UI("坐标")] public string Gps {get; set;}

        public virtual CheckObject CheckObject { get; set; }
        [UI("检查对象名称")]  public string CheckObjectName => CheckObject?.Name ?? string.Empty;
        [UI("安全管理员名称")] public string SafetyAdminName => CheckObject?.SafetyAdminName ?? string.Empty;
        [UI("责任人名称")]    public string DutyUserName => CheckObject?.DutyUserName ?? string.Empty;

        //
        public override object Export(ExportMode mode)
        {
            return new
            {
                Id,
                Name,
                Picture,
                Gps,
                RiskLevel,
                CheckObjectId,
                CheckObjectName,
                SafetyAdminName,
                DutyUserName,
            };
        }

        public static IQueryable<CheckPoint> Search(long? objectId=null, string name="", CheckRiskLevel? riskLevel=null)
        {
            IQueryable<CheckPoint> q = IncludeSet;
            if (objectId.IsNotEmpty())   q = q.Where(o => o.CheckObjectId == objectId.Value);
            if (riskLevel.IsNotEmpty())  q = q.Where(o => o.RiskLevel == riskLevel.Value);
            if (name.IsNotEmpty())       q = q.Where(o => o.Name.Contains(name.Trim()));
            return q;
        }
    }

}