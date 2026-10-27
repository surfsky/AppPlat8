using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    [UI("检查", "对象应急专用标签")]
    public class CheckObjectTagSpec : EntityBase<CheckObjectTagSpec>
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
                tagName = Tag.Name
            };
        }

        public static IQueryable<CheckObjectTagSpec> Search(long? checkObjectId)
        {
            IQueryable<CheckObjectTagSpec> query = IncludeSet;
            if (checkObjectId.IsNotEmpty())
                query = query.Where(item => item.CheckObjectId == checkObjectId.Value);
            return query;
        }
    }
}
