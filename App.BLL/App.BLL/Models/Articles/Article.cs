using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL
{

    /// <summary>文档库管理</summary>
    [UI("OA", "文档")]
    public class Article : EntityBase<Article>
    {
        [UI("文档目录Id")]         public long? CategoryId { get; set; }
        [UI("名称")]              public string Name { get; set; }
        [UI("内容")]              public string Content { get; set; }
        [UI("附件列表")]           public string Attachments { get; set; } // JSON or URLs
        [UI("评论数")]             public int CommentCount { get; set; }
        [UI("是否允许评论")]        public bool AllowComment { get; set; }

        public virtual ArticleDir Category { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Name,
                CategoryId,
                CategoryName = Category?.Name,
                CommentCount,
                AllowComment,
                CreateDt,
                UpdateDt,
                Content
            };
        }

        public static IQueryable<Article> Search(string name, long? categoryId)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())       q = q.Where(o => o.Name.Contains(name.Trim()));
            if (categoryId.IsNotEmpty()) q = q.Where(o => o.CategoryId == categoryId.Value);
            return q;
        }
    }
}
