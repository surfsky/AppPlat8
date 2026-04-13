using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using App.Entities;
using App.Utils;

namespace App.DAL.OA
{
    /// <summary>检查对象事件类别</summary>
    public enum CheckObjectEventType
    {
        [UI("培训")] Train = 1,
        [UI("演练")] Exercise = 2,
        [UI("事故")] Accident = 3,
    }


    [UI("检查", "检查对象事件")]
    public class CheckObjectEvent : EntityBase<CheckObjectEvent>
    {
        [UI("检查对象")]    public long CheckObjectId { get; set; }
        [UI("事件类别")]    public CheckObjectEventType? Type { get; set; }
        [UI("标题")]        public string Title { get; set; }
        [UI("内容")]        public string Content { get; set; }
        [UI("主图")]        public string MainImage { get; set; }
        [UI("发生时间")]     public DateTime? TriggleDt { get; set; }

        public virtual CheckObject CheckObject { get; set; }

        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Type,
                TypeName = Type?.GetTitle(),
                Title,
                Content,
                MainImage,
                CreatorId,
                //CreatorName = Creator?.Name,
                this.CreateDt,
                this.TriggleDt,
            };
        }

        public static IQueryable<CheckObjectEvent> Search(long? checkObjectId, string title, CheckObjectEventType? type)
        {
            var q = IncludeSet.AsQueryable();
            if (checkObjectId.IsNotEmpty()) q = q.Where(o => o.CheckObjectId == checkObjectId.Value);
            if (title.IsNotEmpty())         q = q.Where(o => o.Title.Contains(title.Trim()));
            if (type.IsNotEmpty())          q = q.Where(o => o.Type == type.Value);
            return q;
         }

    }
}
