using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.Utils;
using App.Utils.Gis;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryEdit)]
    public class GeometryImporterModel : AdminModel
    {
        public string TypeTitle { get; set; } = "GIS简单点位";
        public List<string> ColumnHeaders { get; set; } = new List<string> { "名称(Name)", "别称(Alias)", "菜单ID(MenuId)", "地址(Addr)", "经纬度(Gps)", "备注(Remark)", "是否可见(IsVisible)" };
        public long? DefaultMenuId { get; set; }
        public string DefaultCoordType { get; set; } = GisHelper.CoordTypeWgs84;

        private static readonly HashSet<string> NameFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "名称" };
        private static readonly HashSet<string> AliasFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "alias", "别称" };
        private static readonly HashSet<string> MenuIdFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "menuid", "gis菜单id", "菜单id", "所属菜单id" };
        private static readonly HashSet<string> AddrFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "addr", "address", "地址" };
        private static readonly HashSet<string> GpsFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gps", "经纬度", "坐标" };
        private static readonly HashSet<string> RemarkFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "remark", "备注" };
        private static readonly HashSet<string> IsVisibleFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "isvisible", "visible", "show", "是否可见", "可见", "显示" };
        private static readonly HashSet<string> DataJsonFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "datajson", "扩展数据", "属性", "属性json" };
        private static readonly JsonSerializerOptions DataJsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public void OnGet(long? menuId)
        {
            DefaultMenuId = menuId;
            DefaultCoordType = GisHelper.CoordTypeWgs84;
        }

        /// <summary>下载模板</summary>
        public IActionResult OnGetTemplate()
        {
            try
            {
                using var workbook = new XSSFWorkbook();
                
                // 第一个 Sheet：导入模板
                var sheet1 = workbook.CreateSheet("导入模板");
                var headerRow = sheet1.CreateRow(0);
                for (int i = 0; i < ColumnHeaders.Count; i++)
                {
                    headerRow.CreateCell(i).SetCellValue(ColumnHeaders[i]);
                }

                // 添加示例数据
                var exampleRow = sheet1.CreateRow(1);
                exampleRow.CreateCell(0).SetCellValue("示例点位");
                exampleRow.CreateCell(1).SetCellValue("示例别称");
                exampleRow.CreateCell(2).SetCellValue("1"); // 假设1是某个菜单ID
                exampleRow.CreateCell(3).SetCellValue("浙江省杭州市西湖区");
                exampleRow.CreateCell(4).SetCellValue("120.1234, 30.2568");
                exampleRow.CreateCell(5).SetCellValue("这是备注");
                exampleRow.CreateCell(6).SetCellValue("是");
                exampleRow.CreateCell(7).SetCellValue("高");
                exampleRow.CreateCell(8).SetCellValue("一般");
                exampleRow.CreateCell(9).SetCellValue("张三");

                headerRow.CreateCell(7).SetCellValue("风险等级(RiskLevel)");
                headerRow.CreateCell(8).SetCellValue("检查结论(CheckResult)");
                headerRow.CreateCell(9).SetCellValue("联系人");

                // 第二个 Sheet：GIS菜单树
                var sheet2 = workbook.CreateSheet("GIS菜单树");
                var menuHeaderRow = sheet2.CreateRow(0);
                menuHeaderRow.CreateCell(0).SetCellValue("菜单ID");
                menuHeaderRow.CreateCell(1).SetCellValue("菜单名称");
                menuHeaderRow.CreateCell(2).SetCellValue("完整路径");

                var allMenus = GisMenu.Set.OrderBy(m => m.SortId).ToList();
                var menuMap = allMenus.ToDictionary(m => m.Id);
                
                int rowIdx = 1;
                foreach (var menu in allMenus)
                {
                    var row = sheet2.CreateRow(rowIdx++);
                    row.CreateCell(0).SetCellValue(menu.Id);
                    row.CreateCell(1).SetCellValue(menu.Name);
                    
                    // 构建完整路径
                    var path = new List<string>();
                    var cur = menu;
                    while (cur != null)
                    {
                        path.Insert(0, cur.Name);
                        cur = cur.ParentId.HasValue && menuMap.TryGetValue(cur.ParentId.Value, out var p) ? p : null;
                    }
                    row.CreateCell(2).SetCellValue(string.Join(" > ", path));
                }

                using var stream = new MemoryStream();
                workbook.Write(stream);
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "GIS点位导入模板.xlsx");
            }
            catch (Exception ex)
            {
                return BuildResult(400, $"模板生成失败: {ex.Message}");
            }
        }

        /// <summary>执行导入</summary>
        public IActionResult OnPostImport(long? menuId, string coordType = null)
        {
            var logs = new List<string>();
            var file = Request?.Form?.Files?.FirstOrDefault();
            if (file == null || file.Length <= 0)
                return BuildFailResult("请先选择要导入的Excel文件", logs);

            var success = 0;
            var fail = 0;
            var sourceCoordType = GisHelper.NormalizeCoordType(coordType);

            try
            {
                logs.Add($"开始解析文件: {file.FileName}");
                logs.Add($"导入源坐标系: {sourceCoordType}，保存时统一转换为 {GisHelper.CoordTypeWgs84}");
                using var stream = file.OpenReadStream();
                IWorkbook workbook = WorkbookFactory.Create(stream);
                ISheet sheet = workbook.GetSheetAt(0);
                if (sheet == null)
                    return BuildFailResult("Excel文件中未找到工作表", logs);

                IRow headerRow = sheet.GetRow(0);
                if (headerRow == null)
                    return BuildFailResult("Excel文件内容为空", logs);

                // 解析标题行，建立列定义
                var columns = new List<ImportColumn>();
                for (int i = 0; i < headerRow.LastCellNum; i++)
                {
                    var col = ParseColumn(headerRow.GetCell(i), i);
                    if (col != null) columns.Add(col);
                }
                if (columns.Count == 0)
                    return BuildFailResult("Excel表头为空，无法识别导入列", logs);
                
                var userId = GetUserId();
                for (int i = 1; i <= sheet.LastRowNum; i++)
                {
                    IRow row = sheet.GetRow(i);
                    if (row == null || row.Cells.All(c => string.IsNullOrWhiteSpace(c.ToString()))) continue;

                    var rowNo = i + 1;
                    try
                    {
                        var item = new GisGeometry
                        {
                            Type = GeometryType.Point,
                            IsVisible = true,
                            MenuId = menuId,
                            CreatorId = userId,
                            CreateDt = DateTime.Now
                        };

                        var extraData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

                        foreach (var col in columns)
                        {
                            var cellValue = GetCellText(row.GetCell(col.Index));
                            if (string.IsNullOrWhiteSpace(cellValue)) continue;

                            if (col.Kind == ImportFieldKind.Name) item.Name = cellValue;
                            else if (col.Kind == ImportFieldKind.Alias) item.Alias = cellValue;
                            else if (col.Kind == ImportFieldKind.MenuId) item.MenuId = ParseMenuId(cellValue);
                            else if (col.Kind == ImportFieldKind.Addr) item.Addr = cellValue;
                            else if (col.Kind == ImportFieldKind.Gps) item.Gps = cellValue;
                            else if (col.Kind == ImportFieldKind.Remark) item.Remark = cellValue;
                            else if (col.Kind == ImportFieldKind.IsVisible) item.IsVisible = ParseBool(cellValue, col.Title);
                            else if (col.Kind == ImportFieldKind.DataJson) MergeRawJson(extraData, cellValue);
                            else if (col.JsonKey.IsNotEmpty()) extraData[col.JsonKey] = cellValue;
                        }

                        item.DataJson = BuildDataJson(extraData);

                        if (string.IsNullOrWhiteSpace(item.Name))
                            throw new Exception("名称不能为空");

                        // 验证经纬度，并统一转为 WGS84
                        if (!string.IsNullOrEmpty(item.Gps))
                        {
                            if (!GisHelper.TryConvertToWgs84(item.Gps, sourceCoordType, out var gpsWgs84, out var error))
                                throw new Exception(error);
                            item.Gps = gpsWgs84;
                        }

                        item.Save();
                        success++;
                        logs.Add($"第{rowNo}行导入成功: {item.Name}");
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        logs.Add($"第{rowNo}行导入失败: {ex.Message}");
                    }
                }

                var msg = fail == 0
                    ? $"导入完成，成功{success}条"
                    : $"导入完成，成功{success}条，失败{fail}条";

                return BuildResult(0, msg, new
                {
                    total = success + fail,
                    success,
                    fail,
                    logs
                });
            }
            catch (Exception ex)
            {
                logs.Add($"导入失败: {ex.Message}");
                return BuildFailResult("导入失败", logs, success, fail);
            }
        }

        private IActionResult BuildFailResult(string message, List<string> logs, int success = 0, int fail = 0)
        {
            var msg = string.IsNullOrWhiteSpace(message) ? "导入失败" : message;
            if (logs == null) logs = new List<string>();
            if (logs.Count == 0 || logs.Last() != msg) logs.Add(msg);

            return BuildResult(400, msg, new
            {
                success,
                fail,
                logs
            });
        }

        /// <summary>解析导入列</summary>
        private static ImportColumn ParseColumn(ICell cell, int index)
        {
            var header = GetCellText(cell);
            if (header.IsEmpty()) return null;

            var title = header.Trim();
            var key = title;
            var start = title.LastIndexOf('(');
            var end = title.LastIndexOf(')');
            if (start >= 0 && end > start)
            {
                var inside = title.Substring(start + 1, end - start - 1).Trim();
                var outside = title.Substring(0, start).Trim();
                if (inside.IsNotEmpty()) key = inside;
                if (outside.IsNotEmpty()) title = outside;
            }

            var normalized = NormalizeHeaderKey(key);
            return new ImportColumn
            {
                Index = index,
                Header = header,
                Title = title,
                JsonKey = key.Trim(),
                Kind = ResolveFieldKind(normalized)
            };
        }

        /// <summary>解析字段类型</summary>
        private static ImportFieldKind ResolveFieldKind(string key)
        {
            if (NameFields.Contains(key)) return ImportFieldKind.Name;
            if (AliasFields.Contains(key)) return ImportFieldKind.Alias;
            if (MenuIdFields.Contains(key)) return ImportFieldKind.MenuId;
            if (AddrFields.Contains(key)) return ImportFieldKind.Addr;
            if (GpsFields.Contains(key)) return ImportFieldKind.Gps;
            if (RemarkFields.Contains(key)) return ImportFieldKind.Remark;
            if (IsVisibleFields.Contains(key)) return ImportFieldKind.IsVisible;
            if (DataJsonFields.Contains(key)) return ImportFieldKind.DataJson;
            return ImportFieldKind.Extra;
        }

        /// <summary>规范化表头键名</summary>
        private static string NormalizeHeaderKey(string text)
        {
            if (text.IsEmpty()) return string.Empty;
            return text
                .Replace('（', '(')
                .Replace('）', ')')
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Trim()
                .ToLowerInvariant();
        }

        /// <summary>获取单元格文本</summary>
        private static string GetCellText(ICell cell)
        {
            if (cell == null) return string.Empty;
            try
            {
                if (cell.CellType == CellType.Formula)
                {
                    if (cell.CachedFormulaResultType == CellType.Numeric)
                    {
                        if (DateUtil.IsCellDateFormatted(cell))
                            return string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss}", cell.DateCellValue);
                        return Convert.ToString(cell.NumericCellValue, CultureInfo.InvariantCulture) ?? string.Empty;
                    }
                    if (cell.CachedFormulaResultType == CellType.Boolean)
                        return cell.BooleanCellValue ? "true" : "false";
                    return cell.ToString()?.Trim() ?? string.Empty;
                }

                if (cell.CellType == CellType.Numeric)
                {
                    if (DateUtil.IsCellDateFormatted(cell))
                        return string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd HH:mm:ss}", cell.DateCellValue);
                    return Convert.ToString(cell.NumericCellValue, CultureInfo.InvariantCulture) ?? string.Empty;
                }
                if (cell.CellType == CellType.Boolean)
                    return cell.BooleanCellValue ? "true" : "false";
                return cell.ToString()?.Trim() ?? string.Empty;
            }
            catch
            {
                return cell.ToString()?.Trim() ?? string.Empty;
            }
        }

        /// <summary>解析菜单ID</summary>
        private static long? ParseMenuId(string text)
        {
            return long.TryParse(text, out var id) ? id : (long?)null;
        }

        /// <summary>解析布尔值</summary>
        private static bool ParseBool(string text, string fieldName = null)
        {
            var value = (text ?? string.Empty).Trim();
            if (value.IsEmpty())
                return true;

            if (bool.TryParse(value, out var boolVal))
                return boolVal;

            var normalized = value.Replace(" ", string.Empty).ToLowerInvariant();
            if (normalized == "1" || normalized == "是" || normalized == "y" || normalized == "yes" || normalized == "true")
                return true;
            if (normalized == "0" || normalized == "否" || normalized == "n" || normalized == "no" || normalized == "false")
                return false;

            throw new Exception($"{fieldName ?? "布尔字段"} 仅支持 true/false、是/否、1/0");
        }

        /// <summary>合并原始Json文本</summary>
        private static void MergeRawJson(Dictionary<string, object> data, string text)
        {
            if (data == null || text.IsEmpty()) return;
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    throw new Exception("DataJson列必须是Json对象");
                foreach (var item in doc.RootElement.EnumerateObject())
                    data[item.Name] = ExtractJsonValue(item.Value);
            }
            catch (Exception ex)
            {
                throw new Exception($"DataJson格式错误: {ex.Message}");
            }
        }

        /// <summary>提取Json值</summary>
        private static object ExtractJsonValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Number => value.TryGetInt64(out var longVal)
                    ? longVal
                    : value.TryGetDecimal(out var decimalVal) ? decimalVal : value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => JsonSerializer.Deserialize<object>(value.GetRawText())
            };
        }

        /// <summary>构建扩展Json</summary>
        private static string BuildDataJson(Dictionary<string, object> data)
        {
            if (data == null || data.Count == 0) return string.Empty;
            return JsonSerializer.Serialize(data, DataJsonOptions);
        }

        /// <summary>导入列定义</summary>
        private class ImportColumn
        {
            public int Index { get; set; }
            public string Header { get; set; }
            public string Title { get; set; }
            public string JsonKey { get; set; }
            public ImportFieldKind Kind { get; set; }
        }

        /// <summary>导入字段类型</summary>
        private enum ImportFieldKind
        {
            Extra = 0,
            Name = 1,
            Alias = 2,
            MenuId = 3,
            Addr = 4,
            Gps = 5,
            Remark = 6,
            IsVisible = 7,
            DataJson = 8
        }
    }
}
