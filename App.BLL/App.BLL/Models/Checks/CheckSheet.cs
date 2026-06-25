using System.Linq;
using App.Entities;
using App.Utils;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;


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


        [UI("匹配标签"), NotMapped] public List<long> TagIds { get; set; } = new List<long>();
        [UI("标签列表"), NotMapped] public string TagNames { get; set; } = string.Empty;
        [UI("检查项数目"), NotMapped] public int ItemCount { get; set; }
        [UI("匹配的标签")] public virtual List<CheckTag> Tags { get; set; } = new List<CheckTag>(); // n:n关系

        //
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                Scope,
                TagIds,
                CreateDt
            };
        }

        public new static CheckSheet GetDetail(long id)
        {
            return Set
                .Include(o => o.Tags)
                .FirstOrDefault(o => o.Id == id)
                .Let(o =>
                {
                    if (o != null)
                        o.TagIds = o.Tags?.Select(t => t.Id).ToList() ?? new List<long>();
                });
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

        /// <summary>设置匹配的标签列表</summary>
        /// <param name="tagIds">标签ID列表</param>
        public void SetTags(List<long> tagIds, bool save = false)
        {
            tagIds = tagIds.Distinct().ToList();
            var tags = tagIds.Count == 0
                ? new List<CheckTag>()
                : CheckTag.Set.Where(t => tagIds.Contains(t.Id)).ToList();
            Tags.Clear();
            foreach (var tag in tags)
                Tags.Add(tag);
            if (save)
                this.Save();
        }

    }
}