using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using App.Utils;


namespace App.Utils.Gis
{
    /// <summary>经纬度点</summary>
    /// <param name="Lng"></param>
    /// <param name="Lat"></param>
    /// <returns></returns>
    public record LngLat(double Lng, double Lat)
    {
        public static LngLat Parse(string gps)
        {
            if (string.IsNullOrWhiteSpace(gps))
                return null;

            // 统一分隔符，并按分隔符拆分
            var parts = gps.Replace('，', ',').Replace(';', ',').Replace('；', ',').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                // 尝试按空格拆分（某些格式可能用空格分隔）
                parts = gps.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2)
                {
                    // 尝试提取经纬度部分（处理类似 "东经 120.1234, 北纬 30.2568" 这种带中文前缀的情况）
                    var regex = new System.Text.RegularExpressions.Regex(@"(东经|北纬|西经|南纬)?\s*(-?\d+.*?)($|\s|,|，|;|；)");
                    var matches = regex.Matches(gps);
                    if (matches.Count >= 2)
                    {
                        parts = new string[] { matches[0].Value, matches[1].Value };
                    }
                    else
                        return null;
                }
            }

            var lng = ParseValue(parts[0]);
            var lat = ParseValue(parts[1]);

            if (lng.HasValue && lat.HasValue)
            {
                // 如果解析出来的经纬度反了（纬度在前，经度在后），尝试纠正。
                // 经度通常在 72-138 之间（中国范围内），纬度通常在 0-56 之间。
                // 这里简单判断，如果第一个数像纬度且第二个数像经度，则交换。
                if (Math.Abs(lng.Value) < 70 && Math.Abs(lat.Value) > 70)
                    return new LngLat(lat.Value, lng.Value);
                
                return new LngLat(lng.Value, lat.Value);
            }

            return null;
        }

        /// <summary>解析单个坐标值（支持度分秒、带方位后缀等多种格式）</summary>
        private static double? ParseValue(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            val = val.Trim().ToUpper();

            // 提取符号/方向
            double sign = 1;
            if (val.Contains("南纬") || val.Contains("西经") || val.EndsWith("S") || val.EndsWith("W") || val.StartsWith("-"))
                sign = -1;
            
            // 移除所有非数字、非小数点、非度分秒符号的字符
            var cleanVal = System.Text.RegularExpressions.Regex.Replace(val, @"[^\d\.°′'″""\s]", "");
            
            // 尝试直接解析十进制格式
            if (double.TryParse(cleanVal, out double d))
                return d * sign;

            // 解析度分秒格式 (D°M′S″)
            // 120°07.404′
            // 120°07′22″
            var dmsRegex = new System.Text.RegularExpressions.Regex(@"(?<d>\d+(\.\d+)?)°\s*((?<m>\d+(\.\d+)?)['′])?\s*((?<s>\d+(\.\d+)?)[""″])?");
            var match = dmsRegex.Match(val);
            if (match.Success)
            {
                double degrees = double.Parse(match.Groups["d"].Value);
                double minutes = match.Groups["m"].Success ? double.Parse(match.Groups["m"].Value) : 0;
                double seconds = match.Groups["s"].Success ? double.Parse(match.Groups["s"].Value) : 0;
                return (degrees + minutes / 60.0 + seconds / 3600.0) * sign;
            }

            return null;
        }

        public override string ToString() => $"{Lng},{Lat}";
    }
}