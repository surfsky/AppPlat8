using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using App.Entities;
using App.Utils;

namespace App.DAL
{
    /// <summary>
    /// 用户最后活动时间记录表，一个用户一条记录（用户扩展表）。可以合并到 User 表。
    /// </summary>
    public class Online : EntityBase<Online>
    {
        [UI("用户")]        public long UserId { get; set; }
        [UI("最后登陆IP")]   public string LastIP { get; set; }
        [UI("最后登录时间")]  public DateTime LastLoginDt { get; set; }

        public virtual User User { get; set; }
    }
}