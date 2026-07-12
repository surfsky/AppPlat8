using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>联系人目录</summary>
    [UI("OA", "联系人目录")]
    public class ContactMenu : TreeEntity<ContactMenu>
    {
        /// <summary>导出目录树数据</summary>
        public override object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id,
                ParentId,
                Name,
                FullName,
                SortId,
                TreeLevel,
                Children
            };
        }

        /// <summary>搜索联系人目录</summary>
        public static IQueryable<ContactMenu> Search(string name = null, long? parentId = null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())
                q = q.Where(t => t.Name.Contains(name.Trim()));
            if (parentId.IsNotEmpty())
                q = q.Where(t => t.ParentId == parentId.Value);
            return q;
        }
    }
}
