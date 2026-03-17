using System.Linq;
using App.Entities;
using App.Utils;
using Z.EntityFramework.Plus;


/**
检查标签（CheckTag） --(n:n)-- 检查表（CheckSheet）--(1:n)-- 检查项（CheckSheetItem）
*/
namespace App.DAL
{
    /// <summary>检查表项</summary>
    [UI("检查", "检查表项")]
    public class CheckSheetItem : EntityBase<CheckSheetItem>, ISort
    {
        [UI("检查表")] public long SheetId { get; set; }
        [UI("层级")] public CheckRiskLevel RiskLevel { get; set; }
        [UI("名称")] public string Name { get; set; }
        [UI("排序")] public int SortId { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                SheetId,
                RiskLevel,
                RiskLevelName = RiskLevel.GetTitle(),
                Name,
                SortId
            };
        }

        public static IQueryable<CheckSheetItem> Search(long? sheetId=null, CheckRiskLevel? riskLevel=null, string name="")
        {
            IQueryable<CheckSheetItem>  q = CheckSheetItem.IncludeSet;
            if (sheetId.IsNotEmpty())   q = q.Where(o => o.SheetId == sheetId.Value);
            if (riskLevel.IsNotEmpty()) q = q.Where(o => o.RiskLevel == riskLevel.Value);
            if (name.IsNotEmpty())      q = q.Where(o => o.Name.Contains(name.Trim()));
            return q;
        }
    }
}