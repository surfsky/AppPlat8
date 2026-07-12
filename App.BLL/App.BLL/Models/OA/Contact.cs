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
        [UI("电话")] public string Tel { get; set; }
        [UI("组织")] public long? OrgId { get; set; }
        [UI("职务")] public string Title { get; set; }
        [UI("属性")] public string JsonData { get; set; }

        [UI("联系人目录")] public virtual ContactMenu Menu { get; set; }
        [UI("组织")] public virtual Org Org { get; set; }

        [NotMapped] public string MenuName => Menu?.Name;
        [NotMapped] public string OrgName => Org?.Name;

        /// <summary>导出联系人数据</summary>
        public override object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                id = Id,
                menuId = MenuId,
                menuName = MenuName,
                name = Name,
                tel = mode == ExportMode.Detail ? Tel : Tel?.Mask(),
                orgId = OrgId,
                orgName = OrgName,
                title = Title,
                jsonData = JsonData,
                createDt = CreateDt,
                updateDt = UpdateDt
            };
        }

        /// <summary>搜索联系人</summary>
        public static IQueryable<Contact> Search(string name = null, string tel = null, long? orgId = null, long? menuId = null, string title = null)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())
                q = q.Where(t => t.Name.Contains(name.Trim()));
            if (tel.IsNotEmpty())
                q = q.Where(t => t.Tel.Contains(tel.Trim()));
            if (orgId.IsNotEmpty())
                q = q.Where(t => t.OrgId == orgId.Value);
            if (menuId.IsNotEmpty())
                q = q.Where(t => t.MenuId == menuId.Value);
            if (title.IsNotEmpty())
                q = q.Where(t => t.Title.Contains(title.Trim()));
            return q;
        }
    }
}
