using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>事件类别</summary>
    [UI("OA", "事件类别")]
    public class EventType : EntityBase<EventType>, ISort
    {
        [UI("名称")]        public string Name { get; set; }
        [UI("排序")]        public int SortId  {get;set;}

        public static IQueryable<EventType> Search(string name)
        {
            var q = IncludeSet.AsQueryable();
            if (name.IsNotEmpty())          q = q.Where(o => o.Name.Contains(name.Trim()));
            return q.OrderBy(o => o.SortId).ThenBy(o => o.Id);
        }
    }

    /// <summary>事件管理（Event log）</summary>
    [UI("OA", "事件")]
    public class Event : EntityBase<Event>
    {
        [UI("发生时间")]     public DateTime? TriggleDt { get; set; }
        [UI("类别")]        public long? TypeId { get; set; }
        [UI("标题")]        public string Title { get; set; }
        [UI("内容")]        public string Content { get; set; }
        [UI("主图")]        public string MainImage { get; set; }
        [UI("组织")]        public long? OrgId { get; set; }
        [UI("发布人")]      public long? PublisherId { get; set; }
        [UI("是否允许评论")] public bool AllowComment { get; set; }

        public virtual EventType Type { get; set; }
        public virtual Org Org { get; set; }
        public virtual User Publisher { get; set; }

        public string TypeName => Type?.Name;

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                TriggleDt,
                TypeId,
                TypeName,
                Title,
                Content,
                MainImage,
                OrgId,
                OrgName = Org?.Name,
                PublisherId,
                PublisherName = Publisher?.Name,
                AllowComment,
                CreateDt,
            };
        }

        public static IQueryable<Event> Search(string title, long? typeId, long? orgId, long? publisherId)
        {
            var q = IncludeSet.AsQueryable();
            if (title.IsNotEmpty())         q = q.Where(o => o.Title.Contains(title.Trim()));
            if (typeId.IsNotEmpty())       q = q.Where(o => o.TypeId == typeId.Value);
            if (orgId.IsNotEmpty())        q = q.Where(o => o.OrgId == orgId.Value);
            if (publisherId.IsNotEmpty())  q = q.Where(o => o.PublisherId == publisherId.Value);
            return q; //.Sort(t => t.SortId, "DESC");
         }

    }
}
