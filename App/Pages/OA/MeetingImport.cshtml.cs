using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
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
    public class MeetingImportModel : AuthModel
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
                var fileBytes = new byte[file.Length];
                using (var readStream = file.OpenReadStream())
                    readStream.ReadExactly(fileBytes, 0, fileBytes.Length);

                using var workbook = new XSSFWorkbook(new MemoryStream(fileBytes));
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

                var picturesByRow = new Dictionary<int, (byte[] Data, string Ext)>();
                MergePictures(picturesByRow, GetStandardPicturesByRow(sheet, imageColumn));
                MergePictures(picturesByRow, GetDispImgPicturesByRow(fileBytes, sheet, imageColumn));

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
                        if (!TypeMap.TryGetValue(typeText, out var type)) throw new InvalidOperationException($"类别\"{typeText}\"无效");
                        if (!orgsByName.TryGetValue(orgName, out var matchedOrgs)) throw new InvalidOperationException($"未找到科室\"{orgName}\"");
                        if (matchedOrgs.Count != 1) throw new InvalidOperationException($"科室\"{orgName}\"存在重名，无法确定组织");

                        string image = string.Empty;
                        if (picturesByRow.TryGetValue(rowIndex, out var pic))
                        {
                            var mediaType = GetImageMediaType(pic.Ext);
                            var dataUrl = $"data:{mediaType};base64,{Convert.ToBase64String(pic.Data)}";
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

        //======================================================================
        // 列解析
        //======================================================================

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

        //======================================================================
        // 标准 Excel 嵌入图片（NPOI DrawingPatriarch）
        //======================================================================

        private static Dictionary<int, (byte[] Data, string Ext)> GetStandardPicturesByRow(XSSFSheet sheet, int imageColumn)
        {
            var result = new Dictionary<int, (byte[], string)>();
            if (imageColumn < 0 || sheet.DrawingPatriarch is not XSSFDrawing drawing) return result;
            foreach (var shape in drawing.GetShapes().OfType<XSSFPicture>())
            {
                var anchor = shape.GetAnchor() as XSSFClientAnchor;
                if (anchor == null || anchor.Col1 != imageColumn) continue;
                var picData = shape.PictureData;
                result[anchor.Row1] = (picData.Data, picData.SuggestFileExtension());
            }
            return result;
        }

        //======================================================================
        // WPS DISPIMG 单元格内图片（xl/cellimages.xml）
        //======================================================================

        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace WpsNs = "http://www.wps.cn/officeDocument/2017/etCustomData";
        private static readonly XNamespace XdrNs = "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
        private static readonly XNamespace DrawMainNs = "http://schemas.openxmlformats.org/drawingml/2006/main";
        private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PkgRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly Regex DispIdPattern = new(@"ID_[A-F0-9]+", RegexOptions.IgnoreCase);
        private static readonly Regex ColLetterPattern = new(@"([A-Z]+)", RegexOptions.Compiled);

        private static Dictionary<int, (byte[] Data, string Ext)> GetDispImgPicturesByRow(byte[] fileBytes, XSSFSheet sheet, int imageColumn)
        {
            var result = new Dictionary<int, (byte[], string)>();
            if (imageColumn < 0) return result;

            using var archive = new ZipArchive(new MemoryStream(fileBytes), ZipArchiveMode.Read);
            var sheetRelPath = GetSheetRelPath(archive, sheet);
            if (sheetRelPath == null) return result;

            var dispIdsByRow = ParseDispImgIdsByRow(archive, sheetRelPath, imageColumn);
            if (dispIdsByRow.Count == 0) return result;

            var idToRid = ParseCellImageIdToRid(archive);
            if (idToRid.Count == 0) return result;

            var ridToPath = ParseCellImageRidToPath(archive);
            if (ridToPath.Count == 0) return result;

            var mediaPathPrefix = GetCellImageMediaPrefix(archive);

            foreach (var (rowIndex, dispId) in dispIdsByRow)
            {
                if (!idToRid.TryGetValue(dispId, out var rid)) continue;
                if (!ridToPath.TryGetValue(rid, out var mediaPath)) continue;
                var fullPath = mediaPathPrefix + mediaPath.Replace("../", "").Replace('/', '/');
                var ext = Path.GetExtension(mediaPath).TrimStart('.');
                var entry = archive.GetEntry(fullPath);
                if (entry == null) continue;
                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                result[rowIndex] = (ms.ToArray(), ext);
            }

            return result;
        }

        private static string GetSheetRelPath(ZipArchive archive, XSSFSheet sheet)
        {
            var sheetIndex = sheet.Workbook.GetSheetIndex(sheet.SheetName);
            return $"xl/worksheets/sheet{sheetIndex + 1}.xml";
        }

        private static Dictionary<int, string> ParseDispImgIdsByRow(ZipArchive archive, string sheetPath, int imageColumn)
        {
            var result = new Dictionary<int, string>();
            var entry = archive.GetEntry(sheetPath);
            if (entry == null) return result;

            var colLetter = ColumnIndexToLetter(imageColumn);
            using var stream = entry.Open();
            var doc = XDocument.Load(stream);

            foreach (var row in doc.Descendants(MainNs + "row"))
            {
                if (!int.TryParse(row.Attribute("r")?.Value, out var rowNum)) continue;
                var rowIndex = rowNum - 1;

                foreach (var cell in row.Elements(MainNs + "c"))
                {
                    var refAttr = cell.Attribute("r")?.Value ?? "";
                    var match = ColLetterPattern.Match(refAttr);
                    if (!match.Success || match.Groups[1].Value != colLetter) continue;

                    var f = cell.Element(MainNs + "f");
                    if (f == null || string.IsNullOrEmpty(f.Value)) continue;
                    var idMatch = DispIdPattern.Match(f.Value);
                    if (idMatch.Success) result[rowIndex] = idMatch.Value;
                }
            }

            return result;
        }

        private static Dictionary<string, string> ParseCellImageIdToRid(ZipArchive archive)
        {
            var result = new Dictionary<string, string>();
            var entry = archive.GetEntry("xl/cellimages.xml");
            if (entry == null) return result;

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);

            foreach (var cellImage in doc.Descendants(WpsNs + "cellImage"))
            {
                var cNvPr = cellImage.Descendants(XdrNs + "cNvPr").FirstOrDefault();
                var blip = cellImage.Descendants(DrawMainNs + "blip").FirstOrDefault();
                if (cNvPr == null || blip == null) continue;

                var name = cNvPr.Attribute("name")?.Value;
                var rid = blip.Attribute(RelNs + "embed")?.Value;
                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(rid))
                    result[name] = rid;
            }

            return result;
        }

        private static Dictionary<string, string> ParseCellImageRidToPath(ZipArchive archive)
        {
            var result = new Dictionary<string, string>();
            var entry = archive.GetEntry("xl/_rels/cellimages.xml.rels");
            if (entry == null) return result;

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);

            foreach (var rel in doc.Descendants(PkgRelNs + "Relationship"))
            {
                var id = rel.Attribute("Id")?.Value;
                var target = rel.Attribute("Target")?.Value;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target) && !string.Equals(target, "NULL", StringComparison.OrdinalIgnoreCase))
                    result[id] = target;
            }

            return result;
        }

        private static string GetCellImageMediaPrefix(ZipArchive archive) => "xl/";

        private static string ColumnIndexToLetter(int index)
        {
            var result = string.Empty;
            index++;
            while (index > 0)
            {
                index--;
                result = (char)('A' + index % 26) + result;
                index /= 26;
            }
            return result;
        }

        //======================================================================
        // 公共辅助
        //======================================================================

        private static void MergePictures(Dictionary<int, (byte[] Data, string Ext)> target, Dictionary<int, (byte[] Data, string Ext)> source)
        {
            foreach (var item in source)
                target.TryAdd(item.Key, item.Value);
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