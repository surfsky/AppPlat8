using App.Utils;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
//using System.Web.Caching;
//using System.Runtime.Caching;

namespace App.Utils
{
    /// <summary>
    /// IO 辅助方法（文件、路径、程序集）
    /// </summary>
    public static partial class IO
    {
        
        //------------------------------------------------
        // 程序集
        //------------------------------------------------
        /// <summary>获取主入口数据集版本号</summary>
        public static Version AssemblyVersion
        {
            get { return Assembly.GetEntryAssembly().GetName().Version; }
        }

        /// <summary>获取主入口数据集路径</summary>
        public static string AssemblyPath
        {
            get { return Assembly.GetEntryAssembly().Location; }
        }

        /// <summary>获取调用者数据集目录</summary>
        public static string AssemblyDirectory
        {
            get { return new FileInfo(AssemblyPath).DirectoryName; }
        }

        /// <summary>获取某个类型归属的程序集版本号</summary>
        public static Version GetVersion(Type type)
        {
            return type.Assembly.GetName().Version;
        }

        //------------------------------------------------
        // 输出
        //------------------------------------------------
        /// <summary>打印到调试窗口</summary>
        public static void Trace(string format, params object[] args)
        {
            System.Diagnostics.Trace.WriteLine(Util.GetText(format, args));
        }


        /// <summary>打印到控制台窗口</summary>
        public static void Console(string format, params object[] args)
        {
            System.Console.WriteLine(Util.GetText(format, args));
        }

        /// <summary>打印到调试窗口</summary>
        public static void Debug(string format, params object[] args)
        {
            System.Diagnostics.Debug.WriteLine(Util.GetText(format, args));
        }

        /// <summary>打印到所有输出窗口</summary>
        public static void Write(string format, params object[] args)
        {
            Trace(format, args);
            Console(format, args);
            //Debug(format, args);
        }

        

        //------------------------------------------------------------
        // 配置相关 *.config>AppSetting
        //------------------------------------------------------------
        /// <summary>从 .config 文件中获取配置信息</summary>
        //public static T GetConfigSetting<T>(string key)
        //{
        //    var txt = System.Configuration.ConfigurationManager.AppSettings.Get(key);
        //    return txt.Parse<T>();
        //}
        /// <summary>从 .config 文件中获取配置信息</summary>
        public static T GetAppSetting<T>(string key)
        {
            var txt = ConfigurationManager.AppSettings.Get(key);
            return txt.Parse<T>();
        }

        //------------------------------------------------------------
        // 缓存相关
        //------------------------------------------------------------
        //MemoryCache cache = HttpRuntime.Cache;
        //static MemoryCache cache = new MemoryCache(IOption;
        //static MyCacher cache = new MyCacher();

        /// <summary>清除缓存对象</summary>
        //public static void RemoveCache(string key)
        //{
        //    //cache.Remove(key);
        //    //System.Diagnostics.Debug.WriteLine("Clear cache : " + key);
        //    Cacher.
        //}
        //
        ///// <summary>设置缓存对象</summary>
        //public static void SetCache<T>(string key, T value, DateTime? expiredTime=null) where T : class
        //{
        //    cache.Set(key, value, expiredTime);
        //    //System.Diagnostics.Debug.WriteLine("Create cache : " + key);
        //}
        //
        ///// <summary>获取缓存对象（缓存到期后会清空，再次请求时会自动获取）</summary>
        ///// <param name="creator">创建方法。支持若该方法返回值为null</param>
        //public static T GetCache<T>(string key, Func<T> creator=null, DateTime? expiredTime=null) where T : class
        //{
        //    if (creator == null)
        //        return cache[key] as T;
        //    else
        //    {
        //        if (!cache.Contains(key))
        //        {
        //            T o = creator();
        //            SetCache(key, o, expiredTime);
        //        }
        //        return cache[key] as T;
        //    }
        //}


    }
}
