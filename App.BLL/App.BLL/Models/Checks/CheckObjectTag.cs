using System.Linq;
using App.Entities;
using App.Utils;
using Z.EntityFramework.Plus;


/*
检查对象CheckObject --(1:n)-- 检查对象联系人CheckObjectContact
                  --(1:n)-- 检查对象标签CheckTag
*/
namespace App.DAL
{
    /// <summary>对象拥有的标签</summary>
    [UI("检查", "对象拥有的标签")]
    public class CheckObjectTag : EntityBase<CheckObjectTag>
    {
        [UI("检查对象")] public long CheckObjectId { get; set; }
        [UI("标签Id")] public long TagId { get; set; }

        public virtual CheckObject CheckObject { get; set; }
        public virtual CheckTag Tag { get; set; }

        public override object Export(ExportMode mode)
        {
            return new
            {
                tagId = TagId,
                tagName = Tag.Name,
            };
        }

        public static IQueryable<CheckObjectTag> Search(long? checkObjectId)
        {
            IQueryable<CheckObjectTag> q = CheckObjectTag.IncludeSet;
            if (checkObjectId.IsNotEmpty())    q = q.Where(o => o.CheckObjectId == checkObjectId);
            return q;
        }
    }

}