using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    [CheckPower(Power.GisGeometryEdit)]
    public class GeometryImporterModel : AdminModel
    {
        public string TypeTitle { get; set; } = "GIS简单点位";
        public List<string> ColumnHeaders { get; set; } = new List<string> { "名称(Name)", "别称(Alias)", "GIS菜单ID(MenuId)", "地址(Addr)", "经纬度(Gps)", "备注(Remark)" };

        public void OnGet()
        {
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
        public IActionResult OnPostImport()
        {
            var logs = new List<string>();
            var file = Request?.Form?.Files?.FirstOrDefault();
            if (file == null || file.Length <= 0)
                return BuildFailResult("请先选择要导入的Excel文件", logs);

            var success = 0;
            var fail = 0;

            try
            {
                logs.Add($"开始解析文件: {file.FileName}");
                using var stream = file.OpenReadStream();
                IWorkbook workbook = WorkbookFactory.Create(stream);
                ISheet sheet = workbook.GetSheetAt(0);
                if (sheet == null)
                    return BuildFailResult("Excel文件中未找到工作表", logs);

                IRow headerRow = sheet.GetRow(0);
                if (headerRow == null)
                    return BuildFailResult("Excel文件内容为空", logs);

                // 解析标题行，建立列索引映射
                var colMap = new Dictionary<string, int>();
                for (int i = 0; i < headerRow.LastCellNum; i++)
                {
                    var cellValue = headerRow.GetCell(i)?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(cellValue))
                    {
                        // 提取括号内的字段名或直接使用标题
                        var fieldName = cellValue;
                        if (cellValue.Contains("(") && cellValue.Contains(")"))
                        {
                            var start = cellValue.IndexOf("(") + 1;
                            var end = cellValue.IndexOf(")");
                            fieldName = cellValue.Substring(start, end - start).Trim();
                        }
                        colMap[fieldName.ToLower()] = i;
                        colMap[cellValue.ToLower()] = i; // 也支持中文标题匹配
                    }
                }

                // 核心字段
                var coreFields = new[] { "name", "alias", "menuid", "addr", "gps", "remark", "名称", "别称", "gis菜单id", "地址", "经纬度", "备注" };
                
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
                            CreatorId = userId,
                            CreateDt = DateTime.Now
                        };

                        var extraData = new Dictionary<string, string>();

                        foreach (var entry in colMap)
                        {
                            var fieldName = entry.Key;
                            var colIdx = entry.Value;
                            var cellValue = row.GetCell(colIdx)?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(cellValue)) continue;

                            // 映射核心字段
                            if (fieldName == "name" || fieldName == "名称") item.Name = cellValue;
                            else if (fieldName == "alias" || fieldName == "别称") item.Alias = cellValue;
                            else if (fieldName == "menuid" || fieldName == "gis菜单id") item.MenuId = long.TryParse(cellValue, out var mid) ? mid : (long?)null;
                            else if (fieldName == "addr" || fieldName == "地址") item.Addr = cellValue;
                            else if (fieldName == "gps" || fieldName == "经纬度") item.Gps = cellValue;
                            else if (fieldName == "remark" || fieldName == "备注") item.Remark = cellValue;
                            else
                            {
                                // 其它列放入 DataJson
                                // 注意：colMap 中同一个列索引可能对应多个 key (中文和英文)，这里只处理一次
                                var originalTitle = headerRow.GetCell(colIdx).ToString();
                                if (!extraData.ContainsKey(originalTitle))
                                    extraData[originalTitle] = cellValue;
                            }
                        }

                        if (extraData.Count > 0)
                        {
                            item.DataJson = JsonSerializer.Serialize(extraData);
                        }

                        // 验证经纬度
                        if (!string.IsNullOrEmpty(item.Gps))
                        {
                            var lngLat = LngLat.Parse(item.Gps);
                            if (lngLat == null)
                                throw new Exception($"经纬度格式错误: {item.Gps}");
                            // 统一转为标准格式
                            item.Gps = lngLat.ToString();
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
    }
}
