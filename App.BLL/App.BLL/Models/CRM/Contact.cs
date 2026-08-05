using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>通用联系人</summary>
    [UI("OA", "联系人")]
    public class Contact : EntityBase<Contact>
    {
        [UI("联系人目录")] public long? MenuId { get; set; }
        [UI("姓名")] public string Name { get; set; }
        [UI("电话")] public string Phone { get; set; }
        [UI("职务")] public string Title { get; set; }
        [UI("属性")] public string DataJson { get; set; }

        [UI("联系人目录")] public virtual ContactMenu Menu { get; set; }
        [NotMapped] public string MenuName => Menu?.Name;
        [NotMapped] public string PhoneMask => Phone?.Mask();

        /// <summary>导出联系人数据</summary>
        public override object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id,
                MenuId,
                MenuName,
                Name,
                Title,
                DataJson,
                CreateDt,
                UpdateDt,
                Phone = (mode == ExportMode.Detail) ? Phone : PhoneMask,
            };
        }

        /// <summary>搜索联系人</summary>
        public static IQueryable<Contact> Search(string name = null, string tel = null, long? menuId = null, string title = null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())     q = q.Where(t => t.Name.Contains(name.Trim()));
            if (tel.IsNotEmpty())      q = q.Where(t => t.Phone.Contains(tel.Trim()));
            if (menuId.IsNotEmpty())   q = q.Where(t => t.MenuId == menuId.Value);
            if (title.IsNotEmpty())    q = q.Where(t => t.Title.Contains(title.Trim()));
            return q;
        }
    }
}
