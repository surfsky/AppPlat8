using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using App.Entities;
using App.Utils;
using Z.EntityFramework.Plus;
using App.Components;
using System.Collections.Generic;


/**
检查标签（CheckTag） --(n:n)-- 检查表（CheckSheet）--(1:n)-- 检查项（CheckSheetItem）
*/
namespace App.DAL
{
    [UI("检查", "检查表")]
    public class CheckSheet : EntityBase<CheckSheet>
    {
        [UI("名称")] public string Name { get; set; }
        [UI("领域")] public CheckScope Scope { get; set; }

        [UI("匹配的标签")] public virtual List<CheckTag> Tags { get; set; } = new List<CheckTag>(); // n:n关系

        //
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                Scope
            };
        }

        //
        public static IQueryable<CheckSheet> Search(string name="", CheckScope? scope=null, long? tagId=null, List<long> tagIds=null)
        {
            IQueryable<CheckSheet>  q = CheckSheet.IncludeSet; //Set.AsQueryable().Include(o => o.Tags);
            if (name.IsNotEmpty())  q = q.Where(o => o.Name.Contains(name.Trim()));
            if (scope.IsNotEmpty()) q = q.Where(o => o.Scope == scope.Value);
            if (tagId.IsNotEmpty()) q = q.Where(o => o.Tags.Any(t => t.Id == tagId.Value));
            if (tagIds.IsNotEmpty()) q = q.Where(o => o.Tags.Any(t => tagIds.Contains(t.Id)));
            return q;
        }
    }
}