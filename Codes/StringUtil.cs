using System;
using System.Data;
using System.Configuration;
using System.Web;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace App.Components
{
    /// <summary>
    /// 字符串工具类，用于字符串的操作。
    /// </summary>
    public class StringUtil
    {
        /// <summary>将逗号分隔的字符串转换为整数数组</summary>
        /// <param name="commaSeparatedString">逗号分隔的字符串，例如："1,2,3,4,5"</param>
        /// <returns>包含整数的数组</returns>
        public static int[] GetIntArrayFromString(string commaSeparatedString)
        {
            if (String.IsNullOrEmpty(commaSeparatedString))
                return new int[0];
            else
                return commaSeparatedString.Split(',').Select(s => Convert.ToInt32(s)).ToArray();
        }

        //public static string GetJSBeautifyString(string source)
        //{
        //    var jsb = new JSBeautifyLib.JSBeautify(source, new JSBeautifyLib.JSBeautifyOptions());
        //    return jsb.GetResult();
        //}

    }
}
