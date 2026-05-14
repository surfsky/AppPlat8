using System.Globalization;
using System.Text;
using App.DAL;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

var options = ImportOptions.Parse(args);
var report = new List<string>();

try
{
    report.Add($"Start: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    report.Add($"Excel: {options.ExcelPath}");
    report.Add($"DB: {options.DbPath}");
    report.Add($"DryRun: {options.DryRun}");

    if (!File.Exists(options.ExcelPath))
        throw new FileNotFoundException("Excel file not found", options.ExcelPath);

    if (!File.Exists(options.DbPath))
        throw new FileNotFoundException("SQLite db file not found", options.DbPath);

    using var fs = File.OpenRead(options.ExcelPath);
    using var workbook = new XSSFWorkbook(fs);
    var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
    if (sheet == null)
        throw new InvalidOperationException("Excel has no worksheet.");

    var formatter = new DataFormatter(CultureInfo.InvariantCulture);
    var evaluator = workbook.GetCreationHelper().CreateFormulaEvaluator();
    var headerRow = sheet.GetRow(sheet.FirstRowNum) ?? throw new InvalidOperationException("Header row is missing.");

    var headers = ReadHeaders(headerRow, formatter, evaluator);
    var columnPicker = new ColumnPicker(headers);

    var cId = columnPicker.Pick("id", "对象id", "检查对象id");
    var cName = columnPicker.Pick("名称", "企业名称", "单位名称", "对象名称", "场所名称");
    var cSocial = columnPicker.Pick("社会统一信用代码", "统一社会信用代码", "社会信用代码", "信用代码");
    var cGrid = columnPicker.Pick("所属网格", "网格", "所属组织", "所属机构", "责任组织", "归属网格");
    var cLatestCheck = columnPicker.Pick("最新巡查时间", "最新检查时间", "最近巡查时间", "最近检查时间", "最后巡查时间", "巡查时间");
    var cCode = columnPicker.Pick("编码", "对象编码", "编号");
    var cAddress = columnPicker.Pick("地址", "详细地址", "经营地址");
    var cGps = columnPicker.Pick("gps", "经纬度", "坐标", "定位");
    var cField = columnPicker.Pick("领域", "行业领域", "行业");
    var cArchive = columnPicker.Pick("建档日期", "建档时间", "档案日期");
    var cDutyUser = columnPicker.Pick("负责人", "主要负责人", "法定代表人", "法人");
    var cSafetyAdmin = columnPicker.Pick("安全管理员", "安管员", "安全责任人");
    var cEmployee = columnPicker.Pick("员工数", "从业人数", "人员数量", "职工人数");

    report.Add("Detected columns:");
    foreach (var item in columnPicker.Detected)
        report.Add($"  {item.Key} => {(item.Value >= 0 ? headers[item.Value] : "(not found)")}");

    if (cName < 0)
        throw new InvalidOperationException("Column '名称/企业名称/单位名称' is required.");

    var rows = new List<ImportRow>();
    for (var i = sheet.FirstRowNum + 1; i <= sheet.LastRowNum; i++)
    {
        var row = sheet.GetRow(i);
        if (row == null || IsRowEmpty(row, formatter, evaluator))
            continue;

        var data = new ImportRow
        {
            ExcelRow = i + 1,
            SourceId = TryParseLong(ReadCell(row, cId, formatter, evaluator)),
            Name = ReadCell(row, cName, formatter, evaluator),
            SocialCreditCode = NormalizeSocialCredit(ReadCell(row, cSocial, formatter, evaluator)),
            GridPath = ReadCell(row, cGrid, formatter, evaluator),
            LatestCheckDt = ReadDateCell(row, cLatestCheck, formatter, evaluator),
            Code = ReadCell(row, cCode, formatter, evaluator),
            Address = ReadCell(row, cAddress, formatter, evaluator),
            Gps = ReadCell(row, cGps, formatter, evaluator),
            Field = ReadCell(row, cField, formatter, evaluator),
            ArchieveDt = ReadDateCell(row, cArchive, formatter, evaluator),
            DutyUserName = ReadCell(row, cDutyUser, formatter, evaluator),
            SafetyAdminName = ReadCell(row, cSafetyAdmin, formatter, evaluator),
            EmployeeCount = TryParseInt(ReadCell(row, cEmployee, formatter, evaluator)),
        };

        if (string.IsNullOrWhiteSpace(data.Name) && string.IsNullOrWhiteSpace(data.SocialCreditCode) && !data.SourceId.HasValue)
            continue;

        rows.Add(data);
    }

    report.Add($"Rows parsed: {rows.Count}");
    report.Add($"Distinct grid path count: {rows.Select(t => t.GridPath).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    report.Add($"Distinct source id count: {rows.Where(t => t.SourceId.HasValue).Select(t => t.SourceId!.Value).Distinct().Count()}");
    report.Add($"Distinct social credit count: {rows.Where(t => !string.IsNullOrWhiteSpace(t.SocialCreditCode)).Select(t => t.SocialCreditCode).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");
    report.Add($"Distinct unique-key(Social or SourceId) count: {rows.Select(t => BuildUniqueIdentityKey(t)).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).Count()}");

    if (options.DryRun)
    {
        report.Add("Dry run mode, no database changes applied.");
        WriteReport(options.ReportPath, report);
        Console.WriteLine(string.Join(Environment.NewLine, report));
        return;
    }

    var dbOptions = new DbContextOptionsBuilder<AppPlatContext>()
        .UseSqlite($"Data Source={options.DbPath}")
        .Options;

    using var db = new AppPlatContext(dbOptions);

    if (options.EnsureLatestCheckColumn)
    {
        try
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE CheckObjects ADD COLUMN LatestCheckDt TEXT NULL;");
            report.Add("Schema: Added CheckObjects.LatestCheckDt");
        }
        catch (Exception ex)
        {
            var msg = ex.GetBaseException().Message;
            if (msg.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
                report.Add("Schema: CheckObjects.LatestCheckDt already exists");
            else
                throw;
        }
    }

    var orgCache = LoadOrgCache(db);
    var objectById = db.CheckObjects.ToDictionary(t => t.Id, t => t);
    var objectBySocial = db.CheckObjects
        .Where(t => !string.IsNullOrWhiteSpace(t.SocialCreditCode))
        .AsEnumerable()
        .GroupBy(t => NormalizeSocialCredit(t.SocialCreditCode))
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    var objectBySourceId = db.CheckObjects
        .Where(t => !string.IsNullOrWhiteSpace(t.Code))
        .AsEnumerable()
        .GroupBy(t => NormalizeSourceId(t.Code))
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    var inserted = 0;
    var updated = 0;
    var skipped = 0;
    var failed = 0;
    var pendingSaveCount = 0;
    const int saveBatchSize = 300;

    foreach (var row in rows)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(row.Name) && string.IsNullOrWhiteSpace(row.SocialCreditCode) && !row.SourceId.HasValue)
            {
                skipped++;
                report.Add($"Row {row.ExcelRow}: skipped (no identity fields)");
                continue;
            }

            var dutyOrgId = EnsureOrgPath(db, orgCache, row.GridPath);
            CheckObject? item = null;

            if (row.SourceId.HasValue)
            {
                if (objectBySourceId.TryGetValue(NormalizeSourceId(row.SourceId.Value.ToString()), out var bySourceId))
                    item = bySourceId;
            }
            else if (!string.IsNullOrWhiteSpace(row.SocialCreditCode) && objectBySocial.TryGetValue(row.SocialCreditCode, out var bySocial))
            {
                item = bySocial;
            }

            var isNew = item == null;
            item ??= new CheckObject
            {
                InUsed = true,
                IsUsing = true,
                CreateDt = DateTime.Now,
            };

            var changed = false;
            if (!string.IsNullOrWhiteSpace(row.Name) && !string.Equals(item.Name ?? string.Empty, row.Name.Trim(), StringComparison.Ordinal))
            {
                item.Name = row.Name.Trim();
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(row.SocialCreditCode) && !string.Equals(item.SocialCreditCode ?? string.Empty, row.SocialCreditCode.Trim(), StringComparison.Ordinal))
            {
                item.SocialCreditCode = row.SocialCreditCode.Trim();
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(row.Code) && !string.Equals(item.Code ?? string.Empty, row.Code.Trim(), StringComparison.Ordinal))
            {
                item.Code = row.Code.Trim();
                changed = true;
            }
            else if (string.IsNullOrWhiteSpace(item.Code) && row.SourceId.HasValue)
            {
                item.Code = row.SourceId.Value.ToString(CultureInfo.InvariantCulture);
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(row.Address) && !string.Equals(item.Address ?? string.Empty, row.Address.Trim(), StringComparison.Ordinal))
            {
                item.Address = row.Address.Trim();
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(row.Gps) && !string.Equals(item.Gps ?? string.Empty, row.Gps.Trim(), StringComparison.Ordinal))
            {
                item.Gps = row.Gps.Trim();
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(row.Field) && !string.Equals(item.Field ?? string.Empty, row.Field.Trim(), StringComparison.Ordinal))
            {
                item.Field = row.Field.Trim();
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(row.DutyUserName) && !string.Equals(item.DutyUserName ?? string.Empty, row.DutyUserName.Trim(), StringComparison.Ordinal))
            {
                item.DutyUserName = row.DutyUserName.Trim();
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(row.SafetyAdminName) && !string.Equals(item.SafetyAdminName ?? string.Empty, row.SafetyAdminName.Trim(), StringComparison.Ordinal))
            {
                item.SafetyAdminName = row.SafetyAdminName.Trim();
                changed = true;
            }
            if (row.EmployeeCount.HasValue && item.EmployeeCount != row.EmployeeCount)
            {
                item.EmployeeCount = row.EmployeeCount;
                changed = true;
            }
            if (dutyOrgId.HasValue && item.DutyOrgId != dutyOrgId)
            {
                item.DutyOrgId = dutyOrgId;
                changed = true;
            }
            if (row.ArchieveDt.HasValue && item.ArchieveDt != row.ArchieveDt)
            {
                item.ArchieveDt = row.ArchieveDt;
                changed = true;
            }
            if (row.LatestCheckDt.HasValue && item.LatestCheckDt != row.LatestCheckDt)
            {
                item.LatestCheckDt = row.LatestCheckDt;
                changed = true;
            }

            if (isNew)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    skipped++;
                    report.Add($"Row {row.ExcelRow}: skipped (missing Name)");
                    continue;
                }

                db.CheckObjects.Add(item);
                inserted++;
                pendingSaveCount++;
            }
            else if (changed)
            {
                updated++;
                pendingSaveCount++;
            }
            else
            {
                skipped++;
            }

            if (pendingSaveCount >= saveBatchSize)
            {
                db.SaveChanges();
                pendingSaveCount = 0;
            }

            if (item.Id > 0)
                objectById[item.Id] = item;

            if (!string.IsNullOrWhiteSpace(item.SocialCreditCode))
                objectBySocial[NormalizeSocialCredit(item.SocialCreditCode)] = item;

            if (!string.IsNullOrWhiteSpace(item.Code))
                objectBySourceId[NormalizeSourceId(item.Code)] = item;
        }
        catch (Exception ex)
        {
            failed++;
            report.Add($"Row {row.ExcelRow}: failed - {ex.GetBaseException().Message}");
        }
    }

    if (pendingSaveCount > 0)
    {
        db.SaveChanges();
        pendingSaveCount = 0;
    }

    report.Add($"Inserted CheckObject: {inserted}");
    report.Add($"Updated CheckObject: {updated}");
    report.Add($"Skipped rows: {skipped}");
    report.Add($"Failed rows: {failed}");
    report.Add($"Finished: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

    WriteReport(options.ReportPath, report);
    Console.WriteLine(string.Join(Environment.NewLine, report));
}
catch (Exception ex)
{
    report.Add($"Fatal: {ex.GetBaseException().Message}");
    WriteReport(options.ReportPath, report);
    Console.Error.WriteLine(string.Join(Environment.NewLine, report));
    Environment.ExitCode = 1;
}

static List<string> ReadHeaders(IRow row, DataFormatter formatter, IFormulaEvaluator evaluator)
{
    var list = new List<string>();
    for (var i = row.FirstCellNum; i < row.LastCellNum; i++)
        list.Add((formatter.FormatCellValue(row.GetCell(i), evaluator) ?? string.Empty).Trim());
    return list;
}

static bool IsRowEmpty(IRow row, DataFormatter formatter, IFormulaEvaluator evaluator)
{
    for (var i = row.FirstCellNum; i < row.LastCellNum; i++)
    {
        var text = formatter.FormatCellValue(row.GetCell(i), evaluator);
        if (!string.IsNullOrWhiteSpace(text))
            return false;
    }
    return true;
}

static string ReadCell(IRow row, int columnIndex, DataFormatter formatter, IFormulaEvaluator evaluator)
{
    if (columnIndex < 0) return string.Empty;
    var text = formatter.FormatCellValue(row.GetCell(columnIndex), evaluator);
    return (text ?? string.Empty).Trim();
}

static DateTime? ReadDateCell(IRow row, int columnIndex, DataFormatter formatter, IFormulaEvaluator evaluator)
{
    if (columnIndex < 0) return null;
    var cell = row.GetCell(columnIndex);
    if (cell == null) return null;

    if (cell.CellType == CellType.Numeric && DateUtil.IsCellDateFormatted(cell))
        return cell.DateCellValue;

    var text = (formatter.FormatCellValue(cell, evaluator) ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(text))
        return null;

    if (DateTime.TryParse(text, out var dt))
        return dt;

    var formats = new[]
    {
        "yyyy-M-d", "yyyy/M/d", "yyyy.MM.dd", "yyyy-MM-dd", "yyyy/MM/dd",
        "yyyy-M-d H:m", "yyyy-M-d H:m:s", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss",
        "yyyy/MM/dd HH:mm", "yyyy/MM/dd HH:mm:ss"
    };
    if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        return dt;

    return null;
}

static long? TryParseLong(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return null;
    if (long.TryParse(text.Trim(), out var value)) return value;
    return null;
}

static int? TryParseInt(string text)
{
    if (string.IsNullOrWhiteSpace(text)) return null;
    if (int.TryParse(text.Trim(), out var value)) return value;
    return null;
}

static string NormalizeSocialCredit(string? value)
{
    if (string.IsNullOrWhiteSpace(value)) return string.Empty;
    return new string(value.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
}

static string NormalizeSourceId(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
        return string.Empty;

    var raw = value.Trim();
    if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
        return n.ToString(CultureInfo.InvariantCulture);

    return new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}

static string BuildUniqueIdentityKey(ImportRow row)
{
    if (row.SourceId.HasValue)
        return "I:" + row.SourceId.Value.ToString(CultureInfo.InvariantCulture);
    if (!string.IsNullOrWhiteSpace(row.SocialCreditCode))
        return "S:" + NormalizeSocialCredit(row.SocialCreditCode);
    return string.Empty;
}

static Dictionary<string, App.DAL.Org> LoadOrgCache(AppPlatContext db)
{
    return db.Orgs
        .AsNoTracking()
        .ToList()
        .GroupBy(t => BuildOrgKey(t.ParentId, t.Name))
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
}

static long? EnsureOrgPath(AppPlatContext db, Dictionary<string, App.DAL.Org> orgCache, string? gridPath)
{
    if (string.IsNullOrWhiteSpace(gridPath))
        return null;

    var parts = gridPath
        .Split(new[] { '/', '\\', '|', '>', '＞' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(t => t.Trim())
        .Where(t => !string.IsNullOrWhiteSpace(t))
        .ToList();

    if (parts.Count == 0)
        return null;

    long? parentId = null;
    foreach (var part in parts)
    {
        var key = BuildOrgKey(parentId, part);
        if (!orgCache.TryGetValue(key, out var org))
        {
            var nextSort = db.Orgs.Where(t => t.ParentId == parentId).Select(t => (int?)t.SortId).Max() ?? 0;
            org = new App.DAL.Org
            {
                ParentId = parentId,
                Name = part,
                SortId = nextSort + 10,
            };
            db.Orgs.Add(org);
            db.SaveChanges();
            orgCache[key] = org;
        }

        parentId = org.Id;
    }

    return parentId;
}

static string BuildOrgKey(long? parentId, string? name)
{
    var normalizedName = NormalizeHeader(name);
    return $"{parentId?.ToString() ?? "null"}|{normalizedName}";
}

static string NormalizeHeader(string? text)
{
    if (string.IsNullOrWhiteSpace(text))
        return string.Empty;

    var sb = new StringBuilder(text.Length);
    foreach (var ch in text.Trim().ToLowerInvariant())
    {
        if (char.IsLetterOrDigit(ch) || ch >= 0x4e00)
            sb.Append(ch);
    }
    return sb.ToString();
}

static void WriteReport(string reportPath, List<string> lines)
{
    var dir = Path.GetDirectoryName(reportPath);
    if (!string.IsNullOrWhiteSpace(dir))
        Directory.CreateDirectory(dir);

    File.WriteAllLines(reportPath, lines, new UTF8Encoding(false));
}

internal sealed class ColumnPicker
{
    private readonly List<string> _headers;
    private readonly Dictionary<string, int> _normalizedMap;

    public Dictionary<string, int> Detected { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ColumnPicker(List<string> headers)
    {
        _headers = headers;
        _normalizedMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
        {
            var key = NormalizeHeaderLocal(headers[i]);
            if (!_normalizedMap.ContainsKey(key))
                _normalizedMap[key] = i;
        }
    }

    public int Pick(params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var key = NormalizeHeaderLocal(alias);
            if (_normalizedMap.TryGetValue(key, out var idx))
            {
                Detected[alias] = idx;
                return idx;
            }
        }

        foreach (var alias in aliases)
        {
            var key = NormalizeHeaderLocal(alias);
            for (var i = 0; i < _headers.Count; i++)
            {
                if (NormalizeHeaderLocal(_headers[i]).Contains(key, StringComparison.OrdinalIgnoreCase))
                {
                    Detected[alias] = i;
                    return i;
                }
            }
        }

        Detected[aliases[0]] = -1;
        return -1;
    }

    private static string NormalizeHeaderLocal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch) || ch >= 0x4e00)
                sb.Append(ch);
        }
        return sb.ToString();
    }
}

internal sealed class ImportRow
{
    public int ExcelRow { get; set; }
    public long? SourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SocialCreditCode { get; set; } = string.Empty;
    public string GridPath { get; set; } = string.Empty;
    public DateTime? LatestCheckDt { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Gps { get; set; } = string.Empty;
    public string Field { get; set; } = string.Empty;
    public DateTime? ArchieveDt { get; set; }
    public string DutyUserName { get; set; } = string.Empty;
    public string SafetyAdminName { get; set; } = string.Empty;
    public int? EmployeeCount { get; set; }
}

internal sealed class ImportOptions
{
    public required string ExcelPath { get; init; }
    public required string DbPath { get; init; }
    public required string ReportPath { get; init; }
    public bool DryRun { get; init; }
    public bool EnsureLatestCheckColumn { get; init; }

    public static ImportOptions Parse(string[] args)
    {
        var root = FindRepoRoot();
        var excelPath = Path.Combine(root, "Doc", "Data", "260514-对象数据.xlsx");
        var dbPath = Path.Combine(root, "App", "Db", "sqlite.db");
        var reportPath = Path.Combine(root, "Doc", "Data", "260514-对象数据.import.log.txt");
        var dryRun = false;
        var ensureCol = true;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--excel":
                    excelPath = GetValue(args, ref i, arg);
                    break;
                case "--db":
                    dbPath = GetValue(args, ref i, arg);
                    break;
                case "--report":
                    reportPath = GetValue(args, ref i, arg);
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                case "--no-schema-update":
                    ensureCol = false;
                    break;
            }
        }

        return new ImportOptions
        {
            ExcelPath = Path.GetFullPath(excelPath),
            DbPath = Path.GetFullPath(dbPath),
            ReportPath = Path.GetFullPath(reportPath),
            DryRun = dryRun,
            EnsureLatestCheckColumn = ensureCol,
        };
    }

    private static string GetValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"Missing value for {optionName}");
        index++;
        return args[index];
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var sln = Path.Combine(dir.FullName, "AppPlat.sln");
            if (File.Exists(sln))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
