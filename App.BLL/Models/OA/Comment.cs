using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>评论类别</summary>
    public enum CommentType
    {
        [UI("文档")] Article = 1,
        [UI("事件")] Event = 2,
    }


    /// <summary>评论</summary>
    [UI("OA", "评论")]
    public class Comment : EntityBase<Comment>
    {
        [UI("评论内容Id")]   public long? TargetId { get; set; }
        [UI("类别")]        public CommentType? Type { get; set; }
        [UI("评论人")]      public string Author { get; set; }
        [UI("评论内容")]    public string Content { get; set; }
        [UI("评分")]        public int? Rating { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TargetId,
                Type,
                Author,
                Content,
                Rating,
                CreateDt,
            };
        }

        public static IQueryable<Comment> Search(long? targetId, CommentType? type, string author)
        {
            var q = IncludeSet.AsQueryable();
            if (targetId.IsNotEmpty())  q = q.Where(o => o.TargetId == targetId.Value);
            if (type.IsNotEmpty())      q = q.Where(o => o.Type == type.Value);
            if (author.IsNotEmpty())    q = q.Where(o => o.Author.Contains(author.Trim()));
            return q;
         }
    }
}
