using App.Entities;
using App.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.DAL
{
    /// <summary>日志配置</summary>
    /// <remarks>可用该实体控制是否做日志</remarks>
    [UI("系统", "日志配置")]
    public class LogConfig : EntityBase<LogConfig>
    {
        [UI("请求来自")] public string From { get; set; }
        [UI("是否记录")] public bool? Enable { get; set; }


        //-----------------------------------------------
        // 公共方法
        //-----------------------------------------------
        /// <summary>是否需要做日志</summary>
        public static bool NeedLog(string from)
        {
            var cfg = All.FirstOrDefault(t => t.From == from);
            if (cfg == null)
            {
                cfg = new LogConfig() { From = from, Enable = true };
                cfg.Save();
                return true;
            }
            else if (cfg.Enable == false)
                return false;
            return true;
        }
    }
}
