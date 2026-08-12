using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{

    /// <summary>文档库管理</summary>
    [UI("OA", "文档")]
    public class Article : EntityBase<Article>
    {
        [UI("文档目录Id")]         public long? MenuId { get; set; }
        [UI("名称")]              public string Name { get; set; }
        [UI("内容")]              public string Content { get; set; }
        [UI("评论数")]             public int? CommentCount { get; set; }
        [UI("是否允许评论")]        public bool AllowComment { get; set; }

        public virtual ArticleMenu Menu { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                MenuId,
                MenuName = Menu?.Name,
                CommentCount,
                AllowComment,
                CreateDt,
                UpdateDt,
                Content
            };
        }

        public static IQueryable<Article> Search(string name, long? menuId)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())       q = q.Where(o => o.Name.Contains(name.Trim()));
            if (menuId.IsNotEmpty()) q = q.Where(o => o.MenuId == menuId.Value);
            return q;
        }
    }
}
