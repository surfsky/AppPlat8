using System.Linq;
using System.ComponentModel.DataAnnotations.Schema;
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
        [UI("层级")]   public CheckHazardLevel? HazardLevel { get; set; }
        [UI("名称")] public string Name { get; set; }
        [UI("排序")] public int SortId { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                SheetId,
                HazardLevel,
                HazardLevelName = HazardLevel?.GetTitle(),
                Name,
                SortId
            };
        }

        public static IQueryable<CheckSheetItem> Search(long? sheetId = null, CheckHazardLevel? hazardLevel = null, string name = "")
        {
            IQueryable<CheckSheetItem>  q = CheckSheetItem.IncludeSet;
            if (sheetId.IsNotEmpty())     q = q.Where(o => o.SheetId == sheetId.Value);
            if (hazardLevel.IsNotEmpty()) q = q.Where(o => o.HazardLevel == hazardLevel.Value);
            if (name.IsNotEmpty())        q = q.Where(o => o.Name.Contains(name.Trim()));
            return q;
        }
    }
}