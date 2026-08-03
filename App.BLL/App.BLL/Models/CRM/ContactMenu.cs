using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>联系人目录。该类和Org类相似，但Org更为明确是行政机构，而ContactMenu更为宽松；此外，这个类可扩展支持 Company 信息</summary>
    [UI("OA", "联系人目录")]
    public class ContactMenu : TreeEntity<ContactMenu>
    {
        [UI("简称")]             public string AbbrName { get; set; }
        [UI("地址")]             public string Address { get; set; }
        [UI("统一社会信用代码")]   public string SocialCreditId { get; set; }
        [UI("属性")]             public string DataJson { get; set; }

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
                Children,
                DataJson
            };
        }

        /// <summary>搜索联系人目录</summary>
        public static IQueryable<ContactMenu> Search(string name = null, long? parentId = null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())         q = q.Where(t => t.Name.Contains(name.Trim()));
            if (parentId.IsNotEmpty())     
            {
                // 可递归查询子目录
                var ids = GetChildIds(parentId.Value);
                q = q.Where(t => ids.Contains(t.Id));
            }
            return q;
        }
    }
}
