using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    [Auth(AuthLogin = true)]
    public class ImportorModel : AuthModel
    {
        [BindProperty(SupportsGet = true)]      public string Type { get; set; }
        [BindProperty(SupportsGet = true)]      public long? TemplateId { get; set; }
        [BindProperty(SupportsGet = true)]      public string ColumnMode { get; set; } = nameof(ExcelColumnMode.Title);
        [BindProperty(SupportsGet = true)]      public bool IgnoreId { get; set; }

        /// <summary>
        /// 业务主键字段（逗号分隔多字段，如 "Name" / "Name,Code"）：
        /// 导入时按该字段判定是否已存在，存在则更新，不存在则新增。
        /// </summary>
        [BindProperty(SupportsGet = true)]      public string KeyField { get; set; }

        /// <summary>宽松模式：无法识别的单元格置空并提示，该行仍然导入</summary>
        [BindProperty(SupportsGet = true)]      public bool Lenient { get; set; }

        public string TypeTitle { get; set; } = string.Empty;
        public List<string> ColumnHeaders { get; set; } = new List<string>();

        public void OnGet(string type)
        {
            Type = type?.Trim();
            ColumnMode = ParseColumnMode(ColumnMode).ToString();
            if (TryResolveType(Type, out var entityType, out _))
            {
                TypeTitle = entityType.GetTitle();
                ColumnHeaders = BuildColumnHeaders(entityType, IgnoreId);
            }
        }

        /// <summary>获取导入模板</summary>
        /// <param name="type">实体类型</param>
        /// <param name="columnMode">列模式</param>
        /// <param name="templateId">模板附件ID</param>
        /// <param name="ignoreId">是否忽略ID</param>
        /// <returns>模板文件</returns>
        public IActionResult OnGetTemplate(string type, string columnMode, long? templateId, bool ignoreId = false)
        {
            // 从附件ID获取模板文件
            if (templateId != null && templateId > 0)
            {
                var fileResult = BuildTemplateByAttId(templateId.Value);
                if (fileResult != null)
                    return fileResult;
                return BuildResult(404, "模板附件不存在或文件已被删除");
            }

            // 尝试解析实体类型
            if (!TryResolveType(type, out var entityType, out var error))
                return BuildResult(400, error);

            try
            {
                var mode = ParseColumnMode(columnMode);
                var columns = ExcelImporter.GetImportableProperties(entityType, ignoreId);

                // 生成模板文件流（列严格限定为 columns，确保与页面展示的列头一致）
                using var stream = new MemoryStream();
                var parser = CreateParser(entityType, columns);
                var saveMethod = parser.GetType().GetMethod("Save", new[] { typeof(Stream), typeof(ExcelColumnMode) });  // 获取 ExcelParser.Save() 方法
                if (saveMethod != null)
                    saveMethod.Invoke(parser, new object[] { stream, mode });
                else
                    parser.GetType().GetMethod("Save", new[] { typeof(Stream) })?.Invoke(parser, new object[] { stream });
                stream.Position = 0;

                // 生成文件文件名
                var typeTitle = entityType.GetTitle();
                if (string.IsNullOrWhiteSpace(typeTitle))
                    typeTitle = entityType.Name;
                var fileName = $"{typeTitle}_模版.xlsx";
                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                return BuildResult(400, $"模板生成失败: {ex.Message}");
            }
        }

        /// <summary>导入数据</summary>
        /// <param name="type">实体类型</param>
        /// <param name="keyField">业务主键字段（命中则更新，否则新增）</param>
        /// <param name="lenient">宽松模式（无法识别的单元格置空并提示）</param>
        public IActionResult OnPostImport(string type, string keyField = null, bool? lenient = null)
        {
            var logs = new List<string>();
            if (!TryResolveType(type, out var entityType, out var error))
                return BuildFailResult(error, logs);

            var file = Request?.Form?.Files?.FirstOrDefault();
            if (file == null || file.Length <= 0)
                return BuildFailResult("请先选择要导入的Excel文件", logs);

            // keyField / lenient 支持 URL 查询参数（页面 URL 或表单）
            keyField = FirstNonEmpty(
                keyField,
                Request?.Query["keyField"].FirstOrDefault(),
                Request?.Form["keyField"].FirstOrDefault(),
                KeyField);
            var isLenient = lenient ?? ParseBool(Request?.Query["lenient"].FirstOrDefault())
                                    ?? ParseBool(Request?.Form["lenient"].FirstOrDefault())
                                    ?? Lenient;

            try
            {
                logs.Add($"开始解析文件: {file.FileName}" + (string.IsNullOrWhiteSpace(keyField) ? "" : $"（主键字段: {keyField}，存在则更新）"));

                ExcelImportResult result;
                using (var stream = file.OpenReadStream())
                {
                    result = ExcelImporter.Import(entityType, stream, file.FileName, new ExcelImportOptions
                    {
                        KeyField = keyField,
                        Lenient = isLenient,
                        IgnoreId = IgnoreId,
                        UserId = GetUserId(),
                        UrlFieldValues = ExtractUrlFieldValues(entityType)
                    });
                }

                logs.AddRange(result.Logs);

                // 导入审计：记录实体类型、文件名、成功/失败条数
                try
                {
                    string module = entityType.Namespace?.Contains(".DAL") == true ? entityType.Name : $"{entityType.Namespace}/{entityType.Name}";
                    App.Components.Logger.LogImport(module, file.FileName, result.Success, result.Failed);
                }
                catch { }

                return BuildResult(0, result.Message, new
                {
                    total = result.TotalRows,
                    inserted = result.Inserted,
                    updated = result.Updated,
                    success = result.Success,
                    fail = result.Failed,
                    skipped = result.Skipped,
                    logs
                });
            }
            catch (Exception ex)
            {
                logs.Add($"导入失败: {GetInnermostMessage(ex)}");
                return BuildFailResult("导入失败", logs);
            }
        }

        /// <summary>取第一个非空值</summary>
        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))?.Trim();

        /// <summary>解析 bool 字符串</summary>
        private static bool? ParseBool(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var v = value.Trim().ToLowerInvariant();
            if (v == "1" || v == "true" || v == "yes" || v == "on") return true;
            if (v == "0" || v == "false" || v == "no" || v == "off") return false;
            return null;
        }

        /// <summary>根据附件ID获取导入模板</summary>
        /// <param name="templateId">模板附件ID</param>
        /// <returns>模板文件</returns>
        private IActionResult BuildTemplateByAttId(long templateId)
        {
            var item = Att.Get(templateId);
            if (item == null)
                return null;

            var path = App.Web.Asp.MapPath(item.Content);
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                return null;

            var ext = Path.GetExtension(item.FileName ?? item.Content);
            var mimeType = IO.GetMimeType(ext);
            if (string.IsNullOrWhiteSpace(mimeType))
                mimeType = "application/octet-stream";

            var downloadName = string.IsNullOrWhiteSpace(item.FileName)
                ? Path.GetFileName(path)
                : item.FileName;

            return PhysicalFile(path, mimeType, downloadName);
        }

        /// <summary>构建导入失败结果</summary>
        /// <param name="message">失败消息</param>
        /// <param name="logs">导入日志</param>
        /// <param name="success">成功条数</param>
        /// <param name="fail">失败条数</param>
        /// <returns>导入失败结果</returns>
        private IActionResult BuildFailResult(string message, List<string> logs, int success = 0, int fail = 0)
        {
            var msg = string.IsNullOrWhiteSpace(message) ? "导入失败" : message;
            if (logs == null)
                logs = new List<string>();
            if (logs.Count == 0 || logs.Last() != msg)
                logs.Add(msg);

            return BuildResult(400, msg, new
            {
                success,
                fail,
                logs
            });
        }

        /// <summary>
        /// 创建Excel解析器，并将其列严格限定为 props 指定的属性集合，
        /// 确保下载的模板列头与 BuildColumnHeaders 展示的列完全一致。
        /// （实现已抽到 ExcelImporter，模板下载与导入共用同一份逻辑）
        /// </summary>
        private static object CreateParser(Type entityType, List<PropertyInfo> props)
            => ExcelImporter.CreateParser(entityType, props);

        /// <summary>解析列模式</summary>
        /// <param name="value">列模式值</param>
        /// <returns>列模式</returns>
        private static ExcelColumnMode ParseColumnMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ExcelColumnMode.Title;

            var v = value.Trim();
            if (v.Equals("中文标题", StringComparison.OrdinalIgnoreCase) || v.Equals("标题", StringComparison.OrdinalIgnoreCase))
                return ExcelColumnMode.Title;
            if (v.Equals("属性名", StringComparison.OrdinalIgnoreCase) || v.Equals("字段名", StringComparison.OrdinalIgnoreCase))
                return ExcelColumnMode.Property;
            if (v.Equals("都有", StringComparison.OrdinalIgnoreCase) || v.Equals("全部", StringComparison.OrdinalIgnoreCase))
                return ExcelColumnMode.Both;

            if (Enum.TryParse<ExcelColumnMode>(v, true, out var mode))
                return mode;
            return ExcelColumnMode.Title;
        }

        /// <summary>尝试解析实体类型</summary>
        private static bool TryResolveType(string typeName, out Type entityType, out string error)
        {
            error = null;
            entityType = null;

            if (string.IsNullOrWhiteSpace(typeName))
            {
                error = "参数错误: 缺少type";
                return false;
            }

            var type = Reflector.GetType(typeName.Trim());
            if (type == null)
            {
                error = $"未找到类型: {typeName}";
                return false;
            }

            if (!typeof(Entity).IsAssignableFrom(type))
            {
                error = "仅支持实体类型导入";
                return false;
            }

            if (string.IsNullOrWhiteSpace(type.Namespace) || !type.Namespace.StartsWith("App.DAL", StringComparison.Ordinal))
            {
                error = "出于安全考虑，仅允许导入App.DAL命名空间下实体";
                return false;
            }

            entityType = type;
            return true;
        }

        /// <summary>构建列标题</summary>
        /// <param name="entityType">实体类型</param>
        /// <param name="ignoreId">是否忽略ID</param>
        /// <returns>列标题列表</returns>
        private static List<string> BuildColumnHeaders(Type entityType, bool ignoreId)
        {
            var headers = new List<string>();
            var props = ExcelImporter.GetImportableProperties(entityType, ignoreId);

            foreach (var p in props)
            {
                var ui = p.GetCustomAttribute<UIAttribute>();
                var title = string.IsNullOrWhiteSpace(ui?.Title) ? p.Name : ui.Title;
                headers.Add(string.Equals(title, p.Name, StringComparison.Ordinal) ? p.Name : $"{title}({p.Name})");
            }

            return headers;
        }

        /// <summary>
        /// 从当前请求的 URL 查询参数中提取能映射到实体属性的键值对。
        /// 仅保留参数名（忽略大小写）能匹配实体可写属性的项，用于在导入时自动填充外键等字段。
        /// </summary>
        private Dictionary<string, string> ExtractUrlFieldValues(Type entityType)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var writableProps = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var key in Request.Query.Keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                // 跳过 Importor 自身的控制参数
                if (key.Equals("type", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("templateId", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("columnMode", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("ignoreId", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("keyField", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("lenient", StringComparison.OrdinalIgnoreCase)
                    || key.Equals("handler", StringComparison.OrdinalIgnoreCase))
                    continue;

                var val = Request.Query[key].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(val)) continue;
                // 仅当参数名能匹配到实体属性时才收集（如 checkObjectId → CheckObjectId）
                if (writableProps.Contains(key))
                    result[key] = val;
            }
            return result;
        }

        /// <summary>获取异常的最底层消息</summary>
        private static string GetInnermostMessage(Exception ex)
        {
            var e = ex;
            while (e.InnerException != null)
                e = e.InnerException;
            return e.Message;
        }
    }
}
