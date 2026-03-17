using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace App.Utils
{
    /// <summary>
    /// 资源
    /// </summary>
    public static class ResHelp
    {
        /// <summary>获取资源文本</summary>
        /// <remarks>请配置 AppCoreConfig.UseGlobal 和 ResType 属性</remarks>
        //public static string GetResText(this string resName)
        //{
        //    bool useGlobal = UtilConfig.Instance.UseGlobal;
        //    if (useGlobal)
        //        return GetResText(resName, UtilConfig.Instance.GlobalResType);
        //    return resName;
        //}

        /// <summary>获取资源文本</summary>
        /// <param name="resType">资源类。如 App.Properties.Resouce</param>
        //public static string GetResText(this string resName, Type resType)
        //{
        //    if (resType != null)
        //        return new ResourceManager(resType).GetString(resName);
        //    return resName;
        //}

        /// <summary>获取资源文本</summary>
        /// <remarks>请配置 AppCoreConfig.Instance.ApplyGlobal(...)</remarks>
        public static string GetResText(this string resName)
        {
            // 使用全球化资源
            if (UtilConfig.Instance.UseGlobal)
                return GetResText(resName, UtilConfig.Instance.GlobalResType);

            return resName;
        }

        /// <summary>获取资源文本</summary>
        /// <param name="resType">资源类。如 App.Res</param>
        /// <param name="resName">静态资源属性。如 Name</param>
        /// <returns>资源文本信息。若异常则直接返回资源名称</returns>
        public static string GetResText(this string resName, Type resType)
        {
            var p = resType.GetProperty(resName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null)
                return p.GetValue(null).ToString();

            var f = resType.GetField(resName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null)
                return f.GetValue(null).ToString();

            return resName;
        }


        /// <summary>获取资源图片</summary>
        public static Image GetResImage(this string resName, Type resType)
        {
            return new ResourceManager(resType).GetObject(resName) as Image;
        }

        /// <summary>获取资源文件</summary>
        public static byte[] GetResFile(this string resName, Type resType)
        {
            return new ResourceManager(resType).GetObject(resName) as byte[];
        }
    }
}
