using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using App.Utils;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace App.Components
{
    /// <summary>Excel 动态表格存储服务</summary>
    public class AutoExcelStore
    {
        private const int HeaderRowIndex = 0;

        //------------------------------------------------------
        // Public methods
        //------------------------------------------------------
        /**读取工作表模型 */
        public AutoExcelSheet ReadSheet(string file)
        {
            var path = ResolveFilePath(file);
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = OpenWorkbook(fs, path);
            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            if (sheet == null)
                throw new InvalidOperationException("Excel 文件中没有可用工作表");

            var fmt = new DataFormatter(CultureInfo.InvariantCulture);
            var headerRow = sheet.GetRow(HeaderRowIndex);
            if (headerRow == null)
                throw new InvalidOperationException("Excel 第一行为空，无法识别表头");

            var cols = BuildColumns(workbook, sheet, headerRow, fmt);
            var rows = BuildRows(sheet, cols, fmt);
            FillDistinctOptions(cols, rows);

            return new AutoExcelSheet
            {
                File = NormalizeFile(file),
                FileName = Path.GetFileName(path),
                SheetName = sheet.SheetName,
                Columns = cols,
                Rows = rows
            };
        }

        /**分页查询 */
        public AutoExcelQueryResult Query(string file, IDictionary<string, string> filters, int pageIndex, int pageSize)
        {
            var model = ReadSheet(file);
            var all = model.Rows
                .Where(r => MatchFilters(r, model.Columns, filters))
                .ToList();

            var safeSize = pageSize <= 0 ? 20 : pageSize;
            var safeIndex = pageIndex < 0 ? 0 : pageIndex;
            var pager = new Paging
            {
                PageIndex = safeIndex,
                PageSize = safeSize
            }.SetTotal(all.Count);

            var rows = all
                .Skip(safeIndex * safeSize)
                .Take(safeSize)
                .ToList();

            return new AutoExcelQueryResult
            {
                File = model.File,
                FileName = model.FileName,
                SheetName = model.SheetName,
                Columns = model.Columns,
                Rows = rows,
                Pager = pager
            };
        }

        /**读取单行 */
        public AutoExcelRow GetRow(string file, int id)
        {
            if (id <= 0)
                return null;

            var model = ReadSheet(file);
            return model.Rows.FirstOrDefault(r => r.Id == id);
        }

        /**保存单行 */
        public AutoExcelRow SaveRow(string file, int? id, IDictionary<string, string> vals)
        {
            var path = ResolveFilePath(file);
            vals ??= new Dictionary<string, string>();

            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = OpenWorkbook(fs, path);
            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            if (sheet == null)
                throw new InvalidOperationException("Excel 文件中没有可用工作表");

            var fmt = new DataFormatter(CultureInfo.InvariantCulture);
            var headerRow = sheet.GetRow(HeaderRowIndex);
            if (headerRow == null)
                throw new InvalidOperationException("Excel 第一行为空，无法识别表头");

            var cols = BuildColumns(workbook, sheet, headerRow, fmt);
            var rowIndex = ResolveSaveRowIndex(sheet, id);
            var row = sheet.GetRow(rowIndex) ?? sheet.CreateRow(rowIndex);

            CopyRowStyle(sheet, rowIndex, row);
            foreach (var col in cols)
            {
                var txt = vals.TryGetValue(col.Key, out var val) ? val : string.Empty;
                WriteCell(row, col.Index, txt);
            }

            SaveWorkbook(path, workbook);

            return ReadSheet(file).Rows.FirstOrDefault(r => r.Id == rowIndex);
        }

        /**删除单行 */
        public void DeleteRow(string file, int id)
        {
            if (id <= HeaderRowIndex)
                throw new InvalidOperationException("缺少有效行号");

            var path = ResolveFilePath(file);
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var workbook = OpenWorkbook(fs, path);
            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            if (sheet == null)
                throw new InvalidOperationException("Excel 文件中没有可用工作表");

            var row = sheet.GetRow(id);
            if (row == null)
                throw new InvalidOperationException("数据不存在");

            sheet.RemoveRow(row);
            if (id < sheet.LastRowNum)
                sheet.ShiftRows(id + 1, sheet.LastRowNum, -1, true, false);

            SaveWorkbook(path, workbook);
        }

        /**解析文件绝对路径 */
        public string ResolveFilePath(string file)
        {
            var rel = NormalizeFile(file);
            if (string.IsNullOrWhiteSpace(rel))
                throw new InvalidOperationException("缺少 Excel 文件参数");

            var ext = Path.GetExtension(rel)?.ToLowerInvariant();
            if (ext != ".xls" && ext != ".xlsx")
                throw new InvalidOperationException("仅支持 xls/xlsx 文件");

            var root = Path.Combine(Directory.GetCurrentDirectory(), "Files");
            var path = Path.GetFullPath(Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)));
            var rootPath = Path.GetFullPath(root);
            if (!path.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("非法文件路径");
            if (!File.Exists(path))
                throw new InvalidOperationException("Excel 文件不存在");
            return path;
        }

        //------------------------------------------------------
        // Build models
        //------------------------------------------------------
        /**构建列模型 */
        private static List<AutoExcelColumn> BuildColumns(IWorkbook workbook, ISheet sheet, IRow headerRow, DataFormatter fmt)
        {
            var validations = GetColumnValidations(workbook, sheet, fmt);
            var list = new List<AutoExcelColumn>();
            var exists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = headerRow.FirstCellNum; i < headerRow.LastCellNum; i++)
            {
                if (i < 0)
                    continue;

                var title = fmt.FormatCellValue(headerRow.GetCell(i))?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    continue;

                var key = BuildKey(title, i, exists);
                validations.TryGetValue(i, out var options);
                list.Add(new AutoExcelColumn
                {
                    Key = key,
                    Title = title,
                    Index = i,
                    Options = options ?? new List<string>()
                });
            }
            return list;
        }

        /**构建行数据 */
        private static List<AutoExcelRow> BuildRows(ISheet sheet, List<AutoExcelColumn> cols, DataFormatter fmt)
        {
            var list = new List<AutoExcelRow>();
            for (var i = HeaderRowIndex + 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null || RowIsEmpty(row, cols, fmt))
                    continue;

                var cells = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var col in cols)
                    cells[col.Key] = fmt.FormatCellValue(row.GetCell(col.Index))?.Trim() ?? string.Empty;

                list.Add(new AutoExcelRow
                {
                    Id = i,
                    Cells = cells
                });
            }
            return list;
        }

        /**补充离散值选项 */
        private static void FillDistinctOptions(List<AutoExcelColumn> cols, List<AutoExcelRow> rows)
        {
            foreach (var col in cols)
            {
                if (col.Options.Count > 0)
                    continue;

                var options = rows
                    .Select(r => r.Cells.TryGetValue(col.Key, out var val) ? val : string.Empty)
                    .Where(v => !string.IsNullOrWhiteSpace(v))
                    .Distinct()
                    .Take(20)
                    .ToList();

                if (options.Count > 0 && options.Count <= 12)
                    col.Options = options;
            }
        }

        //------------------------------------------------------
        // Read validations
        //------------------------------------------------------
        /**读取列下拉校验 */
        private static Dictionary<int, List<string>> GetColumnValidations(IWorkbook workbook, ISheet sheet, DataFormatter fmt)
        {
            var map = new Dictionary<int, List<string>>();
            var list = sheet.GetDataValidations() ?? new List<IDataValidation>();
            foreach (var item in list)
            {
                var options = ResolveValidationOptions(workbook, item, fmt);
                if (options.Count == 0)
                    continue;

                foreach (var area in item.Regions?.CellRangeAddresses ?? Array.Empty<CellRangeAddress>())
                {
                    for (var col = area.FirstColumn; col <= area.LastColumn; col++)
                    {
                        if (!map.ContainsKey(col))
                            map[col] = options;
                    }
                }
            }
            return map;
        }

        /**解析下拉选项 */
        private static List<string> ResolveValidationOptions(IWorkbook workbook, IDataValidation item, DataFormatter fmt)
        {
            var constraint = item?.ValidationConstraint;
            if (constraint == null)
                return new List<string>();

            var explicitVals = constraint.ExplicitListValues?
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct()
                .ToList();
            if (explicitVals != null && explicitVals.Count > 0)
                return explicitVals;

            var formula = (constraint.Formula1 ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(formula))
                return new List<string>();

            formula = formula.TrimStart('=').Trim();
            if (formula.StartsWith("\"", StringComparison.Ordinal) && formula.EndsWith("\"", StringComparison.Ordinal))
            {
                return formula.Trim('"')
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();
            }

            var name = workbook.GetName(formula);
            if (name != null && !string.IsNullOrWhiteSpace(name.RefersToFormula))
                formula = name.RefersToFormula.Trim().TrimStart('=').Trim();

            return ReadRangeOptions(workbook, formula, fmt);
        }

        /**读取引用区域选项 */
        private static List<string> ReadRangeOptions(IWorkbook workbook, string formula, DataFormatter fmt)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return new List<string>();

            var txt = formula.Trim();
            var bang = txt.IndexOf('!');
            if (bang < 0)
                return new List<string>();

            var sheetName = txt.Substring(0, bang).Trim().Trim('\'');
            var rangeTxt = txt[(bang + 1)..].Trim();
            var sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
                return new List<string>();

            var range = ParseRange(rangeTxt);
            if (range == null)
                return new List<string>();

            var list = new List<string>();
            for (var r = range.FirstRow; r <= range.LastRow; r++)
            {
                for (var c = range.FirstColumn; c <= range.LastColumn; c++)
                {
                    var val = fmt.FormatCellValue(sheet.GetRow(r)?.GetCell(c))?.Trim();
                    if (!string.IsNullOrWhiteSpace(val))
                        list.Add(val);
                }
            }
            return list.Distinct().ToList();
        }

        /**解析区域文本 */
        private static CellRangeAddress ParseRange(string rangeTxt)
        {
            try
            {
                return CellRangeAddress.ValueOf(rangeTxt);
            }
            catch
            {
                return null;
            }
        }

        //------------------------------------------------------
        // Save helpers
        //------------------------------------------------------
        /**解析保存行号 */
        private static int ResolveSaveRowIndex(ISheet sheet, int? id)
        {
            if (id.GetValueOrDefault() > HeaderRowIndex)
                return id.Value;

            return Math.Max(sheet.LastRowNum + 1, HeaderRowIndex + 1);
        }

        /**复制前一行样式 */
        private static void CopyRowStyle(ISheet sheet, int rowIndex, IRow row)
        {
            if (row == null || rowIndex <= HeaderRowIndex + 0)
                return;

            var src = sheet.GetRow(rowIndex - 1);
            if (src == null)
                return;

            row.Height = src.Height;
            for (var i = src.FirstCellNum; i < src.LastCellNum; i++)
            {
                if (i < 0)
                    continue;

                var srcCell = src.GetCell(i);
                if (srcCell == null)
                    continue;

                var cell = row.GetCell(i) ?? row.CreateCell(i);
                cell.CellStyle = srcCell.CellStyle;
            }
        }

        /**写单元格 */
        private static void WriteCell(IRow row, int colIndex, string txt)
        {
            var cell = row.GetCell(colIndex) ?? row.CreateCell(colIndex);
            var val = txt?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(val))
            {
                cell.SetBlank();
                return;
            }

            if (DateTime.TryParse(val, out var dt) && LooksLikeDateCell(cell))
            {
                cell.SetCellValue(dt);
                return;
            }

            if (double.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var num) && LooksLikeNumberCell(cell))
            {
                cell.SetCellValue(num);
                return;
            }

            cell.SetCellValue(val);
        }

        /**判断是否日期格 */
        private static bool LooksLikeDateCell(ICell cell)
        {
            var format = cell?.CellStyle?.GetDataFormatString() ?? string.Empty;
            var txt = format.ToLowerInvariant();
            return txt.Contains("yy") || txt.Contains("mm") || txt.Contains("dd") || txt.Contains("h:");
        }

        /**判断是否数字格 */
        private static bool LooksLikeNumberCell(ICell cell)
        {
            var format = cell?.CellStyle?.GetDataFormatString() ?? string.Empty;
            var txt = format.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(txt))
                return false;
            return !LooksLikeDateCell(cell) && (txt.Contains("0") || txt.Contains("#"));
        }

        //------------------------------------------------------
        // Match / normalize
        //------------------------------------------------------
        /**判断行是否匹配筛选 */
        private static bool MatchFilters(AutoExcelRow row, List<AutoExcelColumn> cols, IDictionary<string, string> filters)
        {
            if (row == null || filters == null || filters.Count == 0)
                return true;

            foreach (var pair in filters)
            {
                var filter = pair.Value?.Trim();
                if (string.IsNullOrWhiteSpace(filter))
                    continue;

                var col = cols.FirstOrDefault(c => c.Key == pair.Key);
                if (col == null)
                    continue;

                row.Cells.TryGetValue(col.Key, out var val);
                val ??= string.Empty;
                if (col.Options.Count > 0)
                {
                    if (!string.Equals(val, filter, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
                else if (val.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        /**判断数据行是否为空 */
        private static bool RowIsEmpty(IRow row, List<AutoExcelColumn> cols, DataFormatter fmt)
        {
            foreach (var col in cols)
            {
                var txt = fmt.FormatCellValue(row.GetCell(col.Index))?.Trim();
                if (!string.IsNullOrWhiteSpace(txt))
                    return false;
            }
            return true;
        }

        /**标准化文件参数 */
        private static string NormalizeFile(string file)
        {
            var txt = (file ?? string.Empty).Trim().Replace('\\', '/');
            if (txt.StartsWith("/"))
                txt = txt.TrimStart('/');
            if (txt.StartsWith("Files/", StringComparison.OrdinalIgnoreCase))
                txt = txt["Files/".Length..];
            if (txt.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException("非法文件路径");
            return txt;
        }

        /**构建列键 */
        private static string BuildKey(string title, int index, ISet<string> exists)
        {
            var baseKey = new string((title ?? string.Empty)
                .Trim()
                .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_')
                .ToArray())
                .Trim('_');

            if (string.IsNullOrWhiteSpace(baseKey))
                baseKey = $"col_{index + 1}";

            var key = baseKey;
            var n = 2;
            while (exists.Contains(key))
                key = $"{baseKey}_{n++}";
            exists.Add(key);
            return key;
        }

        /**打开工作簿 */
        private static IWorkbook OpenWorkbook(Stream stream, string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant();
            if (ext == ".xls")
                return new HSSFWorkbook(stream);
            return new XSSFWorkbook(stream);
        }

        /**保存工作簿 */
        private static void SaveWorkbook(string path, IWorkbook workbook)
        {
            using var ms = new MemoryStream();
            workbook.Write(ms, true);
            File.WriteAllBytes(path, ms.ToArray());
        }
    }

    /// <summary>Excel 页面模型</summary>
    public class AutoExcelSheet
    {
        public string File { get; set; }
        public string FileName { get; set; }
        public string SheetName { get; set; }
        public List<AutoExcelColumn> Columns { get; set; } = new();
        public List<AutoExcelRow> Rows { get; set; } = new();
    }

    /// <summary>Excel 分页结果</summary>
    public class AutoExcelQueryResult
    {
        public string File { get; set; }
        public string FileName { get; set; }
        public string SheetName { get; set; }
        public List<AutoExcelColumn> Columns { get; set; } = new();
        public List<AutoExcelRow> Rows { get; set; } = new();
        public Paging Pager { get; set; } = new();
    }

    /// <summary>Excel 列模型</summary>
    public class AutoExcelColumn
    {
        public string Key { get; set; }
        public string Title { get; set; }
        public int Index { get; set; }
        public List<string> Options { get; set; } = new();
    }

    /// <summary>Excel 行模型</summary>
    public class AutoExcelRow
    {
        public int Id { get; set; }
        public Dictionary<string, string> Cells { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
