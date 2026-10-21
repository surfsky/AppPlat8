using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>事件类别（树结构）</summary>
    [UI("OA", "事件类别")]
    public class EventType : TreeEntity<EventType>
    {
        /// <summary>按名称查询（实体约定方法）</summary>
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
        [UI("发生时间")]     public DateTime? HappenDt { get; set; }
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
        public string OrgName => Org?.Name;
        public string PublisherName => Publisher?.RealName;

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                HappenDt,
                TypeId,
                TypeName,
                Title,
                Content,
                MainImage,
                OrgId,
                OrgName,
                PublisherId,
                PublisherName,
                AllowComment,
                CreateDt,
            };
        }

        public static IQueryable<Event> Search(string title="", long? typeId=null, long? orgId=null, long? publisherId=null, DateTime? happenStartDt=null, DateTime? happenEndDt=null)
        {
            var q = IncludeSet.AsQueryable();
            if (title.IsNotEmpty())            q = q.Where(o => o.Title.Contains(title.Trim()));
            if (happenStartDt.IsNotEmpty())    q = q.Where(o => o.HappenDt >= happenStartDt.Value);
            if (happenEndDt.IsNotEmpty())      q = q.Where(o => o.HappenDt <= happenEndDt.Value);
            if (publisherId.IsNotEmpty())      q = q.Where(o => o.PublisherId == publisherId.Value);
            if (orgId.IsNotEmpty())
            {
                var ids = DAL.Org.GetChildIds(orgId.Value);
                q = q.Where(o => ids.Contains(o.OrgId.Value));
            }
            if (typeId.IsNotEmpty())
            {
                var ids = DAL.EventType.GetChildIds(typeId.Value);
                q = q.Where(o => ids.Contains(o.TypeId.Value));
            }
            return q;
         }

    }
}
