using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;
using Z.EntityFramework.Plus;

namespace App.DAL
{
    /// <summary>公告状态</summary>
    public enum AnnounceStatus
    {
        [UI("草稿")] Draft = 0,
        [UI("已发布")] Published = 1,
        [UI("已关闭")] Closed = 2,
    }

    /// <summary>公告</summary>
    [UI("系统", "公告")]
    public class Announce : EntityBase<Announce>
    {
        [UI("标题")]                                   public string Title { get; set; }
        [UI("创建时间")]                               public DateTime? CreateTime { get; set; }
        [UI("发布时间")]                               public DateTime? PublishTime { get; set; }
        [UI("作者")]                                   public string Author { get; set; }
        [UI("作者Id")]                                 public long? AuthorId { get; set; }
        [UI("内容", Editor = EditorType.Html)]         public string Content { get; set; }
        [UI("摘要")]                                   public string Summary { get; set; }
        [UI("状态")]                                   public AnnounceStatus? Status { get; set; }
        [UI("优先级")]                                 public int? Priority { get; set; }
        [UI("浏览次数")]                               public int? ViewCount { get; set; }
        [UI("是否置顶")]                               public bool? IsTop { get; set; }
        [UI("状态名称")]                               public string StatusName => Status.GetTitle();


        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        public override object Export(ExportMode mode=ExportMode.Normal)
        {
            return new 
            {
                this.Id,
                this.Title,
                this.Author,
                this.Status,
                this.StatusName,
                this.PublishTime,
                this.CreateTime,
                this.Priority,
                this.ViewCount,
                this.IsTop,
                this.Summary,
                this.Content
            };
        }

        /// <summary>查找已发布的公告</summary>
        public static IQueryable<Announce> Search(string title = "", AnnounceStatus? status = null, string author = "", DateTime? fromDt = null, DateTime? toDt = null)
        {
            IQueryable<Announce> q = Set;
            if (status != null)       q = q.Where(a => a.Status == status);
            if (title.IsNotEmpty())   q = q.Where(a => a.Title.Contains(title));
            if (author.IsNotEmpty())  q = q.Where(a => a.Author.Contains(author));
            if (fromDt != null)       q = q.Where(a => a.PublishTime >= fromDt);
            if (toDt != null)         q = q.Where(a => a.PublishTime <= toDt);
            return q;
        }        

        /// <summary>增加浏览次数</summary>
        public static void IncreaseViewCount(long id)
        {
            var announcement = Set.FirstOrDefault(a => a.Id == id);
            if (announcement != null)
            {
                announcement.ViewCount = (announcement.ViewCount ?? 0) + 1;
                announcement.Save();
            }
        }


    }
}
