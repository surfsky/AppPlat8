using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>参考网站</summary>
    [UI("开放平台", "参考网站")]
    public class Site : EntityBase<Site>
    {
        [UI("分组")] public string Type { get; set; }
        [UI("名称")] public string Name { get; set; }
        [UI("描述")] public string Desc { get; set; }
        [UI("网址")] public string Url { get; set; }
        [UI("图标")] public string Icon { get; set; }
        [UI("排序")] public int SortId { get; set; }

        /// <summary>导出</summary>
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Type,
                Name,
                Desc,
                Url,
                Icon,
                SortId,
                CreatorId,
                CreateDt,
                UpdateDt
            };
        }

        /// <summary>查询</summary>
        public static IQueryable<Site> Search(string type = null, string name = null)
        {
            var q = IncludeSet.AsQueryable();
            if (type.IsNotEmpty()) q = q.Where(t => (t.Type ?? "").Contains(type.Trim()));
            if (name.IsNotEmpty()) q = q.Where(t => (t.Name ?? "").Contains(name.Trim()));
            return q.OrderBy(t => t.SortId).ThenBy(t => t.Id);
        }
    }
}
