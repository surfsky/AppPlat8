using System;
using System.Collections.Generic;
//using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App.Utils
{
    /// <summary>
    /// App.Core 类库配置信息
    /// </summary>
    public class UtilConfig
    {
        //---------------------------------------------
        // 单例
        //---------------------------------------------
        private static UtilConfig _cfg;
        public static UtilConfig Instance
        {
            get
            {
                if (_cfg == null)
                    _cfg = new UtilConfig();
                return _cfg;
            }
        }


        //---------------------------------------------
        // 属性
        //---------------------------------------------
        /// <summary>是否启用国际化支持（使用资源文件获取文本）</summary>
        internal bool UseGlobal { get; set; } = false;

        /// <summary>资源类型名称</summary>
        internal Type GlobalResType { get; set; }

        /// <summary>启用国际化支持（使用资源文件获取文本）</summary>
        public void ApplyGlobal(Type resType)
        {
            this.UseGlobal = true;
            this.GlobalResType = resType;
        }


        /// <summary>机器Id（用于SnowflakerId生成）</summary>
        public int MachineId { get; set; } = 1;


        //---------------------------------------------
        // 事件
        //---------------------------------------------
        /// <summary>日志事件</summary>
        public event Action<string, string, int> OnLog;

        /// <summary>做日志（需配置 OnLog 事件)</summary>
        public static void Log(string type, string info, int level=0)
        {
            if (Instance.OnLog != null)
                Instance.OnLog(type, info, level);
        }
    }
}
