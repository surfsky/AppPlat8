using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>导入文件</summary>
    public record GisTyphoonImportFile(string FileName, string Content);

    /// <summary>导入结果</summary>
    public class GisTyphoonImportResult
    {
        public int TyphoonAddCnt { get; set; }
        public int TyphoonEditCnt { get; set; }
        public int LogAddCnt { get; set; }
        public int LogDeleteCnt { get; set; }
        public int FileCnt { get; set; }
        public List<string> Logs { get; set; } = new();
    }

    /// <summary>台风导入器</summary>
    public static class GisTyphoonImporter
    {
        static readonly Dictionary<int, string> _levelMap = new()
        {
            [0] = "低压/减弱",
            [1] = "热带低压",
            [2] = "热带风暴",
            [3] = "强热带风暴",
            [4] = "台风",
            [5] = "强台风",
            [6] = "超强台风",
            [9] = "变性温带气旋"
        };

        /// <summary>获取年份</summary>
        public static int? GetYear(string code)
        {
            var s = (code ?? "").Trim();
            if (s.Length >= 4 && int.TryParse(s[..4], out var year) && year > 1900)
                return year;
            return null;
        }

        /// <summary>导入默认目录</summary>
        public static GisTyphoonImportResult ImportFolder(AppPlatContext db, string metaPath, string dataDir, int minYear = 2016)
        {
            var files = new List<GisTyphoonImportFile>();
            if (File.Exists(metaPath))
                files.Add(new GisTyphoonImportFile(Path.GetFileName(metaPath), File.ReadAllText(metaPath)));

            if (Directory.Exists(dataDir))
            {
                var txtFiles = Directory.GetFiles(dataDir, "CH*BST.txt")
                    .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var file in txtFiles)
                    files.Add(new GisTyphoonImportFile(Path.GetFileName(file), File.ReadAllText(file)));
            }
            return ImportFiles(db, files, minYear);
        }

        /// <summary>导入上传文件</summary>
        public static GisTyphoonImportResult ImportFiles(AppPlatContext db, IEnumerable<GisTyphoonImportFile> files, int minYear = 2016)
        {
            var result = new GisTyphoonImportResult();
            var list = (files ?? Enumerable.Empty<GisTyphoonImportFile>())
                .Where(t => t != null && t.FileName.IsNotEmpty())
                .OrderBy(t => IsMetaFile(t.FileName) ? 0 : 1)
                .ThenBy(t => t.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.FileCnt = list.Count;

            foreach (var file in list)
            {
                if (IsMetaFile(file.FileName))
                {
                    ImportMetaJson(db, file.Content, result, minYear);
                    continue;
                }
                if (TryGetYearFromFile(file.FileName, out var year))
                {
                    if (year >= minYear)
                        ImportYearText(db, file.Content, year, result);
                    continue;
                }
                result.Logs.Add($"跳过文件: {file.FileName}");
            }
            return result;
        }

        /// <summary>导入元数据</summary>
        public static void ImportMetaJson(AppPlatContext db, string json, GisTyphoonImportResult result, int minYear = 2016)
        {
            var list = ParseMetaJson(json, minYear);
            if (list.Count == 0)
            {
                result.Logs.Add("台风元数据为空");
                return;
            }

            var codes = list.Select(t => t.Code).Distinct().ToList();
            var map = db.GisTyphoons.Where(t => codes.Contains(t.Code)).ToDictionary(t => t.Code, t => t);
            foreach (var item in list)
            {
                if (!map.TryGetValue(item.Code, out var ty))
                {
                    ty = new GisTyphoon
                    {
                        Code = item.Code,
                        IsLand = item.IsLand ?? false
                    };
                    db.GisTyphoons.Add(ty);
                    map[item.Code] = ty;
                    result.TyphoonAddCnt++;
                }
                else
                {
                    result.TyphoonEditCnt++;
                }

                ty.Name = PickValue(item.Name, ty.Name);
                ty.ChineseName = PickValue(item.ChineseName, ty.ChineseName);
                ty.BirthUtc = item.BirthUtc ?? ty.BirthUtc;
                ty.DeathUtc = item.DeathUtc ?? ty.DeathUtc;
                ty.MaxLevel = item.MaxLevel ?? ty.MaxLevel;
                ty.IsLand = item.IsLand ?? ty.IsLand ?? false;
            }
            db.SaveChanges();
            result.Logs.Add($"导入台风清单: {list.Count} 条");
        }

        /// <summary>导入年度轨迹</summary>
        public static void ImportYearText(AppPlatContext db, string text, int year, GisTyphoonImportResult result)
        {
            var storms = ParseYearText(text, year)
                .Where(t => t.IsNamed && t.Points.Count > 0)
                .ToList();
            if (storms.Count == 0)
            {
                result.Logs.Add($"年度轨迹为空: {year}");
                return;
            }

            var codes = storms.Select(t => t.Code).Distinct().ToList();
            var tyMap = db.GisTyphoons.Where(t => codes.Contains(t.Code)).ToDictionary(t => t.Code, t => t);
            var oldLogs = db.GisTyphoonLogs.Where(t => codes.Contains(t.Code)).ToList();
            if (oldLogs.Count > 0)
            {
                db.GisTyphoonLogs.RemoveRange(oldLogs);
                result.LogDeleteCnt += oldLogs.Count;
            }

            foreach (var storm in storms)
            {
                var firstUtc = storm.Points.FirstOrDefault()?.TimeUtc;
                var lastUtc = storm.Points.LastOrDefault()?.TimeUtc;
                var maxLevel = GetWindLevel(storm.Points.Max(t => t.WindMs ?? 0));
                if (!tyMap.TryGetValue(storm.Code, out var ty))
                {
                    ty = new GisTyphoon
                    {
                        Code = storm.Code,
                        Name = storm.Name,
                        IsLand = false
                    };
                    db.GisTyphoons.Add(ty);
                    tyMap[storm.Code] = ty;
                    result.TyphoonAddCnt++;
                }
                else
                {
                    result.TyphoonEditCnt++;
                }

                // 优先保留元数据名称，缺失时再回填轨迹头英文名。
                ty.Name = PickValue(ty.Name, storm.Name);
                ty.BirthUtc = MinUtc(ty.BirthUtc, firstUtc);
                ty.DeathUtc = MaxUtc(ty.DeathUtc, lastUtc);
                ty.MaxLevel = Math.Max(ty.MaxLevel ?? 0, maxLevel);

                for (int i = 0; i < storm.Points.Count; i++)
                {
                    var pt = storm.Points[i];
                    db.GisTyphoonLogs.Add(new GisTyphoonLog
                    {
                        Code = storm.Code,
                        TimeUtc = pt.TimeUtc,
                        Lng = pt.Lng,
                        Lat = pt.Lat,
                        Pressure = pt.Pressure,
                        WindMs = pt.WindMs,
                        LevelCode = pt.LevelCode,
                        LevelName = pt.LevelName,
                        SortId = i + 1
                    });
                    result.LogAddCnt++;
                }
            }
            db.SaveChanges();
            result.Logs.Add($"导入年度轨迹: {year}，台风 {storms.Count} 个，轨迹 {storms.Sum(t => t.Points.Count)} 条");
        }

        /// <summary>解析元数据 JSON</summary>
        static List<GisTyphoonMetaItem> ParseMetaJson(string json, int minYear)
        {
            var list = new List<GisTyphoonMetaItem>();
            if (json.IsEmpty())
                return list;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var code = GetString(item, "code", "id");
                var year = GetYear(code);
                if (code.IsEmpty() || !year.HasValue || year.Value < minYear)
                    continue;

                list.Add(new GisTyphoonMetaItem
                {
                    Code = code,
                    Name = GetString(item, "name"),
                    ChineseName = GetString(item, "chineseName", "chinesName", "chinaname"),
                    BirthUtc = ParseUtc(GetString(item, "birthUtc")),
                    DeathUtc = ParseUtc(GetString(item, "deathUtc")),
                    MaxLevel = GetInt(item, "maxLevel"),
                    IsLand = GetBool(item, "isLand", "IsLand")
                });
            }
            return list;
        }

        /// <summary>解析年度轨迹文本</summary>
        static List<GisTyphoonStorm> ParseYearText(string text, int year)
        {
            var list = new List<GisTyphoonStorm>();
            GisTyphoonStorm storm = null;
            var lines = (text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var raw in lines)
            {
                var line = (raw ?? "").Trim();
                if (line.IsEmpty())
                    continue;
                if (line.StartsWith("66666"))
                {
                    if (storm != null && storm.Points.Count > 0)
                        list.Add(storm);
                    storm = ParseHeaderLine(line, year);
                    continue;
                }
                if (storm == null)
                    continue;

                var pt = ParseTrackLine(line);
                if (pt != null)
                    storm.Points.Add(pt);
            }
            if (storm != null && storm.Points.Count > 0)
                list.Add(storm);
            return list;
        }

        /// <summary>解析头记录</summary>
        static GisTyphoonStorm ParseHeaderLine(string line, int year)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var shortCode = GetPart(parts, 1);
            var seqCode = GetPart(parts, 3);
            var rawCode = GetPart(parts, 4);
            var rawName = GetPart(parts, 7);
            if (rawCode.IsEmpty() || rawCode == "0000")
                rawCode = shortCode;

            var isNamed = Regex.IsMatch(rawCode ?? "", @"^\d{4}$") && rawCode != "0000" && !string.Equals(rawName, "(nameless)", StringComparison.OrdinalIgnoreCase);
            var code = isNamed
                ? $"{year}{rawCode[^2..]}"
                : $"{year}N{SafeTail(seqCode, 2)}";
            return new GisTyphoonStorm
            {
                Year = year,
                Code = code,
                Name = string.Equals(rawName, "(nameless)", StringComparison.OrdinalIgnoreCase) ? "" : rawName,
                IsNamed = isNamed
            };
        }

        /// <summary>解析轨迹点</summary>
        static GisTyphoonTrackPoint ParseTrackLine(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 6)
                return null;
            var timeCode = GetPart(parts, 0);
            if (!Regex.IsMatch(timeCode ?? "", @"^\d{10}$"))
                return null;

            var lat = ToDouble(GetPart(parts, 2)) / 10;
            var lng = ToDouble(GetPart(parts, 3)) / 10;
            if (!lat.HasValue || !lng.HasValue)
                return null;

            var levelCode = ToInt(GetPart(parts, 1));
            var windMs = ToInt(GetPart(parts, 5));
            return new GisTyphoonTrackPoint
            {
                TimeUtc = ParseUtcTimeCode(timeCode),
                Lat = lat,
                Lng = lng,
                Pressure = ToInt(GetPart(parts, 4)),
                WindMs = windMs,
                LevelCode = levelCode,
                LevelName = GetLevelName(levelCode, windMs)
            };
        }

        /// <summary>是否元数据文件</summary>
        static bool IsMetaFile(string fileName)
        {
            return string.Equals(Path.GetFileName(fileName), "typhoons.json", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>尝试从文件名取年份</summary>
        static bool TryGetYearFromFile(string fileName, out int year)
        {
            year = 0;
            var name = Path.GetFileName(fileName) ?? "";
            var match = Regex.Match(name, @"CH(?<year>\d{4})BST\.txt", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups["year"].Value, out year);
        }

        /// <summary>获取等级名</summary>
        static string GetLevelName(int? levelCode, int? windMs)
        {
            if (levelCode.HasValue && _levelMap.TryGetValue(levelCode.Value, out var name))
                return name;
            if ((windMs ?? 0) >= 51) return "超强台风";
            if ((windMs ?? 0) >= 41) return "强台风";
            if ((windMs ?? 0) >= 33) return "台风";
            if ((windMs ?? 0) >= 25) return "强热带风暴";
            if ((windMs ?? 0) >= 18) return "热带风暴";
            if ((windMs ?? 0) > 0) return "热带低压";
            return "低压/减弱";
        }

        /// <summary>估算风力等级</summary>
        static int GetWindLevel(int windMs)
        {
            if (windMs >= 62) return 18;
            if (windMs >= 56) return 17;
            if (windMs >= 51) return 16;
            if (windMs >= 46) return 15;
            if (windMs >= 41) return 14;
            if (windMs >= 37) return 13;
            if (windMs >= 33) return 12;
            if (windMs >= 28) return 10;
            if (windMs >= 18) return 8;
            if (windMs > 0) return 6;
            return 0;
        }

        /// <summary>解析 UTC 时间</summary>
        static DateTime? ParseUtc(string raw)
        {
            var s = (raw ?? "").Trim();
            if (s.IsEmpty() || s.Contains("待更新"))
                return null;

            var fmts = new[]
            {
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy/MM/dd HH:mm",
                "yyyy/MM/dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssZ"
            };
            if (DateTime.TryParseExact(s, fmts, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return null;
        }

        /// <summary>解析时间码</summary>
        static DateTime? ParseUtcTimeCode(string code)
        {
            if (DateTime.TryParseExact(code, "yyyyMMddHH", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return null;
        }

        /// <summary>选取非空值</summary>
        static string PickValue(string value, string oldValue)
        {
            return value.IsNotEmpty() ? value.Trim() : oldValue;
        }

        /// <summary>取较早时间</summary>
        static DateTime? MinUtc(DateTime? a, DateTime? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return a.Value <= b.Value ? a : b;
        }

        /// <summary>取较晚时间</summary>
        static DateTime? MaxUtc(DateTime? a, DateTime? b)
        {
            if (!a.HasValue) return b;
            if (!b.HasValue) return a;
            return a.Value >= b.Value ? a : b;
        }

        /// <summary>读取字符串</summary>
        static string GetString(JsonElement item, params string[] names)
        {
            foreach (var name in names)
            {
                if (item.TryGetProperty(name, out var value))
                    return value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() : value.ToString().Trim();
            }
            return "";
        }

        /// <summary>读取整数</summary>
        static int? GetInt(JsonElement item, params string[] names)
        {
            foreach (var name in names)
            {
                if (!item.TryGetProperty(name, out var value))
                    continue;
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i))
                    return i;
                if (int.TryParse(value.ToString(), out i))
                    return i;
            }
            return null;
        }

        /// <summary>读取布尔值</summary>
        static bool? GetBool(JsonElement item, params string[] names)
        {
            foreach (var name in names)
            {
                if (!item.TryGetProperty(name, out var value))
                    continue;
                if (value.ValueKind == JsonValueKind.True) return true;
                if (value.ValueKind == JsonValueKind.False) return false;
                var s = value.ToString().Trim().ToLowerInvariant();
                if (s is "1" or "true" or "yes") return true;
                if (s is "0" or "false" or "no") return false;
            }
            return null;
        }

        /// <summary>读取数组项</summary>
        static string GetPart(string[] parts, int idx)
        {
            return idx >= 0 && idx < parts.Length ? (parts[idx] ?? "").Trim() : "";
        }

        /// <summary>安全截取尾部</summary>
        static string SafeTail(string text, int len)
        {
            var s = (text ?? "").Trim();
            if (s.Length <= len) return s;
            return s[^len..];
        }

        /// <summary>转整数</summary>
        static int? ToInt(string text)
        {
            return int.TryParse((text ?? "").Trim(), out var val) ? val : null;
        }

        /// <summary>转小数</summary>
        static double? ToDouble(string text)
        {
            return double.TryParse((text ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val) ? val : null;
        }

        class GisTyphoonMetaItem
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string ChineseName { get; set; }
            public DateTime? BirthUtc { get; set; }
            public DateTime? DeathUtc { get; set; }
            public int? MaxLevel { get; set; }
            public bool? IsLand { get; set; }
        }

        class GisTyphoonStorm
        {
            public int Year { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public bool IsNamed { get; set; }
            public List<GisTyphoonTrackPoint> Points { get; set; } = new();
        }

        class GisTyphoonTrackPoint
        {
            public DateTime? TimeUtc { get; set; }
            public double? Lng { get; set; }
            public double? Lat { get; set; }
            public int? Pressure { get; set; }
            public int? WindMs { get; set; }
            public int? LevelCode { get; set; }
            public string LevelName { get; set; }
        }
    }
}
