using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using App.Utils;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace App.Components
{
    public enum ExcelColumnMode
    {
        Title,
        Property,
        Both
    }

    /// <summary>
    /// 基于类型定义的 Excel 读写工具。
    /// 支持按属性名、UI 标题、组合列头匹配字段。
    /// </summary>
    public class ExcelParser<T> where T : class, new()
    {
        private readonly List<ExcelColumn> _columns;
        private readonly Func<PropertyInfo, bool> _propertyFilter;

        public ExcelParser() : this(null)
        {
        }

        public ExcelParser(Func<PropertyInfo, bool> propertyFilter)
        {
            _propertyFilter = propertyFilter;
            _columns = BuildColumns();
        }

        public List<T> Fetch(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));
            using var fs = File.OpenRead(filePath);
            return Fetch(fs, filePath);
        }

        public List<T> Fetch(Stream stream)
        {
            return Fetch(stream, string.Empty);
        }

        public List<T> Fetch(Stream stream, string fileNameHint)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var workbook = CreateWorkbookForRead(stream, fileNameHint);
            var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
            if (sheet == null)
                return new List<T>();

            var headerRow = sheet.GetRow(sheet.FirstRowNum);
            if (headerRow == null)
                return new List<T>();

            var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
            var headerMap = BuildHeaderMap(headerRow, evaluator);
            if (headerMap.Count == 0)
                throw new InvalidOperationException("未匹配到可导入列，请检查表头。");

            var list = new List<T>();
            for (var i = sheet.FirstRowNum + 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null || RowIsEmpty(row))
                    continue;

                var item = new T();
                foreach (var kv in headerMap)
                {
                    var colIndex = kv.Key;
                    var col = kv.Value;
                    var cell = row.GetCell(colIndex);

                    if (!TryConvertCell(cell, evaluator, col.Property.PropertyType, out var value, out var error))
                    {
                        var colNo = colIndex + 1;
                        var rowNo = i + 1;
                        throw new FormatException($"第{rowNo}行第{colNo}列({col.HeaderDisplay})解析失败: {error}");
                    }

                    col.Property.SetValue(item, value);
                }
                list.Add(item);
            }

            return list;
        }

        public void Save(string filePath)
        {
            Save(filePath, null, ExcelColumnMode.Title);
        }

        public void Save(string filePath, ExcelColumnMode columnMode)
        {
            Save(filePath, null, columnMode);
        }

        public void Save(string filePath, IEnumerable<T> items)
        {
            Save(filePath, items, ExcelColumnMode.Title);
        }

        public void Save(string filePath, IEnumerable<T> items, ExcelColumnMode columnMode)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("文件路径不能为空", nameof(filePath));

            using var fs = File.Create(filePath);
            Save(fs, items, filePath, columnMode);
        }

        public void Save(Stream stream)
        {
            Save(stream, null, string.Empty, ExcelColumnMode.Title);
        }

        public void Save(Stream stream, ExcelColumnMode columnMode)
        {
            Save(stream, null, string.Empty, columnMode);
        }

        public void Save(Stream stream, IEnumerable<T> items)
        {
            Save(stream, items, string.Empty, ExcelColumnMode.Title);
        }

        public void Save(Stream stream, IEnumerable<T> items, ExcelColumnMode columnMode)
        {
            Save(stream, items, string.Empty, columnMode);
        }

        private void Save(Stream stream, IEnumerable<T> items, string fileNameHint, ExcelColumnMode columnMode)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var workbook = CreateWorkbookForWrite(fileNameHint);
            var sheet = workbook.CreateSheet(GetSheetName());

            var headerRow = sheet.CreateRow(0);
            for (var i = 0; i < _columns.Count; i++)
            {
                headerRow.CreateCell(i).SetCellValue(GetHeaderText(_columns[i], columnMode));
            }

            var data = items?.ToList() ?? new List<T>();
            for (var i = 0; i < data.Count; i++)
            {
                var row = sheet.CreateRow(i + 1);
                for (var c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    var cell = row.CreateCell(c);
                    var val = col.Property.GetValue(data[i]);
                    WriteCell(cell, val, col.Property.PropertyType);
                }
            }

            // For template downloads, provide one editable sample row to reduce user setup steps.
            if (data.Count == 0)
            {
                var row = sheet.CreateRow(1);
                for (var c = 0; c < _columns.Count; c++)
                {
                    var cell = row.CreateCell(c);
                    var options = GetValidationOptionTitles(_columns[c].Property.PropertyType);
                    if (options.Count > 0)
                        cell.SetCellValue(options[0]);
                    else
                        cell.SetCellValue(string.Empty);
                }
            }

            ApplyEnumDropDownValidation(workbook, sheet, dataStartRowIndex: 1, dataEndRowIndex: 1000);

            for (var i = 0; i < _columns.Count; i++)
            {
                sheet.AutoSizeColumn(i);
                // Keep headers readable in Office/WPS: apply minimum width and extra padding.
                var current = sheet.GetColumnWidth(i);
                var widened = Math.Max(current + 1024, 20 * 256);
                sheet.SetColumnWidth(i, Math.Min(widened, 120 * 256));
            }

            // Keep caller stream open (template download relies on reading stream after Save).
            workbook.Write(stream, true);
            stream.Flush();
        }

        private void ApplyEnumDropDownValidation(IWorkbook workbook, ISheet sheet, int dataStartRowIndex, int dataEndRowIndex)
        {
            if (workbook == null || sheet == null || dataStartRowIndex > dataEndRowIndex)
                return;

            const string dictSheetName = "_dropdown_options";
            var dictSheet = workbook.GetSheet(dictSheetName) ?? workbook.CreateSheet(dictSheetName);
            var helper = sheet.GetDataValidationHelper();
            var dictColIndex = 0;

            for (var i = 0; i < _columns.Count; i++)
            {
                var options = GetValidationOptionTitles(_columns[i].Property.PropertyType);
                if (options.Count == 0)
                    continue;

                for (var r = 0; r < options.Count; r++)
                {
                    var row = dictSheet.GetRow(r) ?? dictSheet.CreateRow(r);
                    var cell = row.GetCell(dictColIndex) ?? row.CreateCell(dictColIndex);
                    cell.SetCellValue(options[r]);
                }

                var colRef = CellReference.ConvertNumToColString(dictColIndex);
                var rangeName = $"_enum_{typeof(T).Name}_{i}";
                var name = workbook.GetName(rangeName) ?? workbook.CreateName();
                name.NameName = rangeName;
                name.RefersToFormula = $"'{dictSheetName}'!${colRef}$1:${colRef}${options.Count}";

                var address = new CellRangeAddressList(dataStartRowIndex, dataEndRowIndex, i, i);
                var constraint = helper.CreateFormulaListConstraint(rangeName);
                var validation = helper.CreateValidation(constraint, address);
                if (validation is XSSFDataValidation)
                    validation.SuppressDropDownArrow = true;
                else
                    validation.SuppressDropDownArrow = false;
                validation.ShowErrorBox = true;
                validation.CreateErrorBox("输入不合法", "请从下拉列表中选择有效枚举值");
                sheet.AddValidationData(validation);

                dictColIndex++;
            }

            if (dictColIndex > 0)
            {
                var dictSheetIndex = workbook.GetSheetIndex(dictSheet);
                if (dictSheetIndex >= 0)
                    workbook.SetSheetHidden(dictSheetIndex, SheetState.Hidden);
            }
        }

        private static List<string> GetValidationOptionTitles(Type propType)
        {
            var realType = Nullable.GetUnderlyingType(propType) ?? propType;

            if (realType == typeof(bool))
                return new List<string> { "是", "否" };

            if (!realType.IsEnum)
                return new List<string>();

            return Enum.GetValues(realType)
                .Cast<object>()
                .Select(v => v.GetTitle())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();
        }

        private static string GetHeaderText(ExcelColumn col, ExcelColumnMode columnMode)
        {
            if (columnMode == ExcelColumnMode.Property)
                return col.PropertyName;
            if (columnMode == ExcelColumnMode.Both)
                return col.HeaderDisplay;
            return col.Title;
        }

        private List<ExcelColumn> BuildColumns()
        {
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                .Where(p => IsSupportedType(p.PropertyType))
            .Where(p => _propertyFilter == null || _propertyFilter(p))
                .ToList();

            var list = new List<ExcelColumn>();
            foreach (var p in props)
            {
                var ui = p.GetCustomAttribute<UIAttribute>();
                if (ui != null && ui.ReadOnly)
                    continue;

                var title = ui?.Title;
                if (string.IsNullOrWhiteSpace(title))
                    title = p.Name;

                var headerDisplay = string.Equals(title, p.Name, StringComparison.Ordinal)
                    ? p.Name
                    : $"{title}({p.Name})";

                var keys = new List<string>
                {
                    p.Name,
                    title,
                    ui?.FullTitle,
                    headerDisplay,
                    $"{title}（{p.Name}）"
                }
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(Normalize)
                .Distinct()
                .ToList();

                list.Add(new ExcelColumn
                {
                    Property = p,
                    PropertyName = p.Name,
                    Title = title,
                    HeaderDisplay = headerDisplay,
                    MatchKeys = keys
                });
            }

            return list;
        }

        private Dictionary<int, ExcelColumn> BuildHeaderMap(IRow headerRow, IFormulaEvaluator evaluator)
        {
            var keyMap = new Dictionary<string, ExcelColumn>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in _columns)
            {
                foreach (var key in col.MatchKeys)
                {
                    if (!keyMap.ContainsKey(key))
                        keyMap[key] = col;
                }
            }

            var map = new Dictionary<int, ExcelColumn>();
            for (var i = headerRow.FirstCellNum; i < headerRow.LastCellNum; i++)
            {
                var raw = GetCellText(headerRow.GetCell(i), evaluator);
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                foreach (var key in ExpandHeaderKeys(raw))
                {
                    if (keyMap.TryGetValue(key, out var col))
                    {
                        map[i] = col;
                        break;
                    }
                }
            }
            return map;
        }

        private static IEnumerable<string> ExpandHeaderKeys(string raw)
        {
            var list = new List<string> { Normalize(raw) };
            AddBracketPart(list, raw, '(', ')');
            AddBracketPart(list, raw, '（', '）');
            return list.Distinct();
        }

        private static void AddBracketPart(List<string> list, string raw, char left, char right)
        {
            var s = raw ?? string.Empty;
            var l = s.IndexOf(left);
            var r = s.LastIndexOf(right);
            if (l >= 0 && r > l)
            {
                var head = s.Substring(0, l);
                var inner = s.Substring(l + 1, r - l - 1);
                if (!string.IsNullOrWhiteSpace(head))
                    list.Add(Normalize(head));
                if (!string.IsNullOrWhiteSpace(inner))
                    list.Add(Normalize(inner));
            }
        }

        private static bool TryConvertCell(ICell cell, IFormulaEvaluator evaluator, Type targetType, out object value, out string error)
        {
            error = null;
            value = null;

            var realType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            var text = GetCellText(cell, evaluator)?.Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                if (Nullable.GetUnderlyingType(targetType) != null || targetType == typeof(string))
                {
                    value = null;
                    return true;
                }

                value = GetDefaultValue(realType);
                return true;
            }

            try
            {
                if (realType == typeof(string))
                {
                    value = text;
                    return true;
                }

                if (realType == typeof(bool))
                {
                    if (TryParseBool(text, out var b))
                    {
                        value = b;
                        return true;
                    }

                    error = $"无法识别布尔值 '{text}'";
                    return false;
                }

                if (realType == typeof(DateTime))
                {
                    if (cell != null && cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
                    {
                        value = cell.DateCellValue;
                        return true;
                    }

                    if (DateTime.TryParse(text, out var dt))
                    {
                        value = dt;
                        return true;
                    }

                    error = $"无法识别日期 '{text}'";
                    return false;
                }

                if (realType.IsEnum)
                {
                    if (TryParseEnum(realType, text, out var enumValue))
                    {
                        value = enumValue;
                        return true;
                    }

                    error = $"无法识别枚举值 '{text}'";
                    return false;
                }

                if (TryParseNumber(realType, text, out var numberVal))
                {
                    value = numberVal;
                    return true;
                }

                value = Convert.ChangeType(text, realType, CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryParseNumber(Type realType, string text, out object value)
        {
            value = null;
            var style = NumberStyles.Any;
            var culture = CultureInfo.InvariantCulture;

            if (realType == typeof(int) && int.TryParse(text, style, culture, out var i)) { value = i; return true; }
            if (realType == typeof(long) && long.TryParse(text, style, culture, out var l)) { value = l; return true; }
            if (realType == typeof(short) && short.TryParse(text, style, culture, out var s)) { value = s; return true; }
            if (realType == typeof(uint) && uint.TryParse(text, style, culture, out var ui)) { value = ui; return true; }
            if (realType == typeof(ulong) && ulong.TryParse(text, style, culture, out var ul)) { value = ul; return true; }
            if (realType == typeof(ushort) && ushort.TryParse(text, style, culture, out var us)) { value = us; return true; }
            if (realType == typeof(float) && float.TryParse(text, style, culture, out var f)) { value = f; return true; }
            if (realType == typeof(double) && double.TryParse(text, style, culture, out var d)) { value = d; return true; }
            if (realType == typeof(decimal) && decimal.TryParse(text, style, culture, out var m)) { value = m; return true; }
            return false;
        }

        private static bool TryParseEnum(Type enumType, string text, out object value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var num))
            {
                try
                {
                    var raw = Convert.ChangeType(num, Enum.GetUnderlyingType(enumType), CultureInfo.InvariantCulture);
                    value = Enum.ToObject(enumType, raw);
                    return true;
                }
                catch
                {
                }
            }

            if (Enum.TryParse(enumType, text, true, out var named))
            {
                value = named;
                return true;
            }

            var normalized = Normalize(text);
            foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (Normalize(field.Name) == normalized)
                {
                    value = field.GetValue(null);
                    return true;
                }

                var ui = field.GetCustomAttribute<UIAttribute>();
                if (ui != null && Normalize(ui.Title) == normalized)
                {
                    value = field.GetValue(null);
                    return true;
                }

                var desc = field.GetCustomAttribute<DescriptionAttribute>();
                if (desc != null && Normalize(desc.Description) == normalized)
                {
                    value = field.GetValue(null);
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseBool(string text, out bool value)
        {
            value = false;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var t = text.Trim().ToLowerInvariant();
            if (t == "1" || t == "true" || t == "yes" || t == "y" || t == "是" || t == "对" || t == "√")
            {
                value = true;
                return true;
            }

            if (t == "0" || t == "false" || t == "no" || t == "n" || t == "否" || t == "错" || t == "×")
            {
                value = false;
                return true;
            }

            return bool.TryParse(text, out value);
        }

        private static void WriteCell(ICell cell, object value, Type propType)
        {
            if (value == null)
            {
                cell.SetCellValue(string.Empty);
                return;
            }

            var realType = Nullable.GetUnderlyingType(propType) ?? propType;
            if (realType == typeof(DateTime))
            {
                cell.SetCellValue((DateTime)value);
                return;
            }

            if (realType == typeof(bool))
            {
                cell.SetCellValue((bool)value ? "是" : "否");
                return;
            }

            if (realType.IsEnum)
            {
                cell.SetCellValue(value.GetTitle());
                return;
            }

            if (realType == typeof(int) || realType == typeof(long) || realType == typeof(short)
                || realType == typeof(uint) || realType == typeof(ulong) || realType == typeof(ushort)
                || realType == typeof(float) || realType == typeof(double) || realType == typeof(decimal))
            {
                if (double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var d))
                {
                    cell.SetCellValue(d);
                    return;
                }
            }

            cell.SetCellValue(Convert.ToString(value));
        }

        private static object GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private static bool IsSupportedType(Type type)
        {
            var realType = Nullable.GetUnderlyingType(type) ?? type;
            return realType.IsBasicType();
        }

        private static string GetCellText(ICell cell, IFormulaEvaluator evaluator)
        {
            if (cell == null)
                return string.Empty;

            var fmt = new DataFormatter(CultureInfo.InvariantCulture);
            return fmt.FormatCellValue(cell, evaluator);
        }

        private static bool RowIsEmpty(IRow row)
        {
            if (row == null)
                return true;

            for (var i = row.FirstCellNum; i < row.LastCellNum; i++)
            {
                var cell = row.GetCell(i);
                if (cell == null)
                    continue;
                if (cell.CellType == CellType.Blank)
                    continue;
                if (!string.IsNullOrWhiteSpace(cell.ToString()))
                    return false;
            }
            return true;
        }

        private static IWorkbook CreateWorkbookForRead(Stream stream, string fileNameHint)
        {
            if (stream.CanSeek)
                stream.Position = 0;

            var ext = Path.GetExtension(fileNameHint ?? string.Empty)?.ToLowerInvariant();
            if (ext == ".xlsx")
                return new XSSFWorkbook(stream);
            if (ext == ".xls")
                return new HSSFWorkbook(stream);

            // 无后缀或后缀不可信时，优先尝试 xlsx，再回退 xls
            if (!stream.CanSeek)
                throw new InvalidOperationException("无法自动识别Excel格式：流不支持定位");

            try
            {
                stream.Position = 0;
                return new XSSFWorkbook(stream);
            }
            catch
            {
                stream.Position = 0;
                return new HSSFWorkbook(stream);
            }
        }

        private static IWorkbook CreateWorkbookForWrite(string fileNameHint)
        {
            var ext = Path.GetExtension(fileNameHint ?? string.Empty)?.ToLowerInvariant();
            if (ext == ".xls")
                return new HSSFWorkbook();
            return new XSSFWorkbook();
        }

        private static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return text
                .Trim()
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty)
                .Replace("（", "(")
                .Replace("）", ")")
                .ToLowerInvariant();
        }

            private static string GetSheetName()
            {
                var type = typeof(T);
                var title = type.GetCustomAttribute<UIAttribute>()?.Title;
                var raw = string.IsNullOrWhiteSpace(title) ? type.Name : title;
                return WorkbookUtil.CreateSafeSheetName(raw);
            }

        private class ExcelColumn
        {
            public PropertyInfo Property { get; set; }
            public string PropertyName { get; set; }
            public string Title { get; set; }
            public string HeaderDisplay { get; set; }
            public List<string> MatchKeys { get; set; }
        }
    }
}