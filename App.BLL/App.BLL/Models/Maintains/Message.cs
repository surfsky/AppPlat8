using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using App.Entities;
using App.Utils;


namespace App.DAL
{
    /// <summary>消息类别</summary>
    public enum MessageType : int
    {
        [UI("系统", "通知")]     Notify = 0,
        [UI("系统", "任务")]     Task = 1,
        [UI("用户", "留言")]     Guest = 10,
        [UI("用户", "互发")]     User = 11,
    }

    /// <summary>消息通道</summary>
    public enum MessageWay
    {
        [UI("网页")]  Web = 0,
        [UI("短信")]  SMS = 1,
        [UI("邮件")]  Mail = 2,
        [UI("APP")]   App = 3,
        [UI("微信")]  Wechat = 4,
        [UI("钉钉")]  Ding = 5,
    }

    /// <summary>非即时性、数据量不大、通知性质的消息</summary>
    [UI("系统", "非即时性消息")]
    public class Message : EntityBase<Message>
    {
        [UI("消息类别")]  public MessageType? Type { get; set; }
        [UI("消息通道")]  public MessageWay? Way { get; set; }
        [UI("标题")]      public string Title { get; set; }
        [UI("内容")]      public string Content { get; set; }
        [UI("URL")]       public string URL { get; set; }
        [UI("From")]      public long?   SenderID { get; set; }
        [UI("To")]        public string  Receivers { get; set; }

        [UI("预约时间")]  public DateTime? AssignDt { get; set; }
        [UI("发送时间")]  public DateTime? SendDt { get; set; }
        [UI("是否成功")]  public bool? IsSuccess { get; set; }

        public virtual User Sender { get; set; }

        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        // 查询
        public static IQueryable<Message> Search(MessageType? type, string title, DateTime? startDt, DateTime? endDt, long? senderID)
        {
            IQueryable<Message> q = Set.Include(t => t.Sender);
            if (type != null)                  q = q.Where(t => t.Type == type);
            if (title.IsNotEmpty())            q = q.Where(t => t.Title.Contains(title));
            if (startDt != null)               q = q.GreaterEqual(t => t.CreateDt.Value, startDt.Value);
            if (endDt != null)                 q = q.LessEqual(t => t.CreateDt.Value, endDt.Value);
            if (senderID != null)              q = q.Where(t => t.SenderID == senderID);
            return q;
        }
    }
}