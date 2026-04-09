using App.Utils;
//using EntityFramework.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Newtonsoft.Json;
using App.Entities;

namespace App.DAL
{
    /// <summary>IP 黑白名单设置</summary>
    [UI("系统", "IP 黑白名单设置")]
    public class IPFilter : EntityBase<IPFilter>, ICacheAll
    {
        [UI("名称")]     public string IP { get; set; }
        [UI("封禁时间")] public DateTime? StartDt { get; set; }
        [UI("解禁时间")] public DateTime? EndDt { get; set; }
        [UI("备注")]     public string Remark { get; set; }
        [UI("地址")]     public string Addr { get; set; }

        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        // 查询
        public static IQueryable<DAL.IPFilter> Search(string ip = null)
        {
            IQueryable<IPFilter> q = Set;
            if (ip.IsNotEmpty())  q = q.Where(t => t.IP == ip);
            return q;
        }

        //-----------------------------------------------
        // 缓存及判断逻辑
        //-----------------------------------------------
        /// <summary>指定 IP 是否被禁止</summary>
        public static bool IsBanned(string ip)
        {
            foreach (var filter in All)
            {
                if (filter.IP == ip)
                {
                    var now = DateTime.Now;
                    if (filter.EndDt == null)      return true;  // 结束时间为空，则永远封禁
                    else if (filter.EndDt > now)   return true;  // 尚未到解禁时间
                    return false;
                }
            }
            return false;
        }

        /// <summary>禁止指定IP访问网站</summary>
        /// <param name="minutes">封禁分钟数。如果为空，则永久封禁</param>
        public static void Ban(string ip, string info, int? minutes)
        {
            var now = DateTime.Now;
            var filter = IPFilter.Set.FirstOrDefault(t => t.IP == ip);
            if (filter == null)
                filter = new IPFilter() { IP = ip };
            filter.StartDt = now;
            filter.EndDt = (minutes == null) ? (DateTime?)null : now.AddMinutes(minutes.Value);
            filter.Remark = info;
            filter.Save();
        }

    }
}