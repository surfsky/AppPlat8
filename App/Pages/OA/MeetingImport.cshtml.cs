using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace App.Pages.OA
{
    [Auth(Power.MeetingImport)]
    public class MeetingImportModel : AdminModel
    {
        private static readonly Dictionary<string, MeetingType> TypeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["周会"] = MeetingType.Week,
            ["月会"] = MeetingType.Month,
            ["培训会"] = MeetingType.Training,
            ["其它"] = MeetingType.Other,
            ["其他"] = MeetingType.Other
        };

        public void OnGet() { }

        public IActionResult OnPostImport()
        {
            if (!CheckPower(Power.MeetingImport)) return BuildResult(403, "无权导入");
            var file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0) return BuildResult(400, "请选择 Excel 文件");
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BuildResult(400, "仅支持 .xlsx 格式的会议导入文件");

            var logs = new List<string>();
            var success = 0;
            var failed = 0;
            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XSSFWorkbook(stream);
                var sheet = workbook.GetSheetAt(0) as XSSFSheet;
                if (sheet == null) return BuildResult(400, "Excel 中没有工作表");

                var headerRow = sheet.GetRow(sheet.FirstRowNum);
                var columns = GetColumns(headerRow);
                if (!columns.TryGetValue("日期", out var dayColumn)
                    || !columns.TryGetValue("类别", out var typeColumn)
                    || !columns.TryGetValue("科室", out var orgColumn)
                    || !columns.TryGetValue("内容", out var contentColumn))
                {
                    return BuildResult(400, "Excel 必须包含：日期、类别、科室、内容列");
                }

                columns.TryGetValue("照片", out var imageColumn);
                var picturesByRow = GetPicturesByRow(sheet, imageColumn);
                var orgsByName = App.DAL.Org.All
                    .Where(org => !string.IsNullOrWhiteSpace(org.Name))
                    .GroupBy(org => org.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

                for (var rowIndex = sheet.FirstRowNum + 1; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (IsEmpty(row)) continue;
                    var displayRow = rowIndex + 1;
                    try
                    {
                        var day = GetDate(row?.GetCell(dayColumn));
                        var typeText = GetText(row?.GetCell(typeColumn));
                        var orgName = GetText(row?.GetCell(orgColumn));
                        var content = GetText(row?.GetCell(contentColumn));
                        if (!day.HasValue) throw new InvalidOperationException("日期格式无效");
                        if (!TypeMap.TryGetValue(typeText, out var type)) throw new InvalidOperationException($"类别“{typeText}”无效");
                        if (!orgsByName.TryGetValue(orgName, out var matchedOrgs)) throw new InvalidOperationException($"未找到科室“{orgName}”");
                        if (matchedOrgs.Count != 1) throw new InvalidOperationException($"科室“{orgName}”存在重名，无法确定组织");

                        string image = string.Empty;
                        if (picturesByRow.TryGetValue(rowIndex, out var picture))
                        {
                            var mediaType = GetImageMediaType(picture.SuggestFileExtension());
                            var dataUrl = $"data:{mediaType};base64,{Convert.ToBase64String(picture.Data)}";
                            image = Uploader.SaveFile(nameof(Meeting), dataUrl);
                            if (string.IsNullOrEmpty(image)) throw new InvalidOperationException("照片保存失败");
                        }

                        new Meeting
                        {
                            Day = day.Value.Date,
                            Type = type,
                            OrgId = matchedOrgs[0].Id,
                            Content = content,
                            Image = image
                        }.Save();
                        success++;
                        logs.Add($"第 {displayRow} 行导入成功");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        logs.Add($"第 {displayRow} 行导入失败：{ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                return BuildResult(400, $"导入失败：{ex.Message}", new { logs, success, failed });
            }

            var message = failed == 0 ? $"导入完成，成功 {success} 条" : $"导入完成，成功 {success} 条，失败 {failed} 条";
            return BuildResult(0, message, new { logs, success, failed });
        }

        private static Dictionary<string, int> GetColumns(IRow headerRow)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (headerRow == null) return result;
            for (var column = headerRow.FirstCellNum; column < headerRow.LastCellNum; column++)
            {
                var title = GetText(headerRow.GetCell(column));
                if (!string.IsNullOrWhiteSpace(title)) result[title.Trim()] = column;
            }
            return result;
        }

        private static Dictionary<int, IPictureData> GetPicturesByRow(XSSFSheet sheet, int imageColumn)
        {
            var result = new Dictionary<int, IPictureData>();
            if (imageColumn < 0 || sheet.DrawingPatriarch is not XSSFDrawing drawing) return result;
            foreach (var shape in drawing.GetShapes().OfType<XSSFPicture>())
            {
                var anchor = shape.GetAnchor() as XSSFClientAnchor;
                if (anchor == null || anchor.Col1 != imageColumn) continue;
                result[anchor.Row1] = shape.PictureData;
            }
            return result;
        }

        private static bool IsEmpty(IRow row) => row == null || row.Cells.All(cell => string.IsNullOrWhiteSpace(GetText(cell)));

        private static string GetText(ICell cell)
        {
            if (cell == null) return string.Empty;
            return cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell)
                ? cell.DateCellValue.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : cell.ToString()?.Trim() ?? string.Empty;
        }

        private static DateTime? GetDate(ICell cell)
        {
            if (cell == null) return null;
            if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell)) return cell.DateCellValue;
            return DateTime.TryParse(GetText(cell), CultureInfo.CurrentCulture, DateTimeStyles.None, out var value) ? value : null;
        }

        private static string GetImageMediaType(string extension) => extension?.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            _ => "image/png"
        };
    }
}