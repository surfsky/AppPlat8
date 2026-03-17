using Microsoft.International.Converters.PinYinConverter;
using System.Text.RegularExpressions;

namespace App.Utils
{
    /// <summary>汉语拼音相关</summary>
    /// <remarks>老的代码 netcore 失败，故改用 Microsoft.International.Converters.PinYinConverter 包</remarks>
    public static class PinYin
    {
        /// <summary>汉字转换成全拼的拼音</summary>
        /// <param name="chineseText">汉字字符串</param>
        /// <returns>转换后的拼音字符串</returns>
        public static string ToPinYin(this string chineseText)
        {
            var result = string.Empty;
            foreach (char item in chineseText)
            {
                try
                {
                    var cc = new ChineseChar(item);
                    if (cc.Pinyins.Count > 0 && cc.Pinyins[0].Length > 0)
                    {
                        string temp = cc.Pinyins[0].ToString();                     // 如 NI3。只取第一种读音（只能凑合着用，以后再想办法优化）
                        result += temp.Substring(0, temp.Length - 1).ToHighCamel(); // 去掉声调，首字母大写
                    }
                }
                catch
                {
                    result += item.ToString();
                }
            }
            return result;
        }

        /// <summary>汉字转换成拼音首字母</summary>
        public static string ToPinYinCap(this string chineseText)
        {
            if (chineseText.IsEmpty())
                return "";
            var pinyin = ToPinYin(chineseText);
            var cap = "";
            for (int i = 0; i < pinyin.Length; i++)
            {
                var c = pinyin[i];
                if (c >= 'A' && c <= 'Z')
                    cap += c;
            }
            return cap;
        }
    }
}