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
                using var stream = new MemoryStream();  /// 模板文件流
                var mode = ParseColumnMode(columnMode);
                var parser = CreateParser(entityType, ignoreId);
                var saveMethod = parser.GetType().GetMethod("Save", new[] { typeof(Stream), typeof(ExcelColumnMode) });  // 获取 Save() 方法
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
        public IActionResult OnPostImport(string type)
        {
            var logs = new List<string>();
            if (!TryResolveType(type, out var entityType, out var error))
                return BuildFailResult(error, logs);

            var file = Request?.Form?.Files?.FirstOrDefault();
            if (file == null || file.Length <= 0)
                return BuildFailResult("请先选择要导入的Excel文件", logs);

            var success = 0;
            var fail = 0;

            try
            {
                logs.Add($"开始解析文件: {file.FileName}");
                var parser = CreateParser(entityType);
                using var stream = file.OpenReadStream();
                var fetchMethod = parser.GetType().GetMethod("Fetch", new[] { typeof(Stream), typeof(string) });
                var raw = fetchMethod?.Invoke(parser, new object[] { stream, file.FileName }) as IEnumerable;
                var rows = raw?.Cast<object>().ToList() ?? new List<object>();

                logs.Add($"解析完成，共{rows.Count}条记录");

                var saveMethod = entityType.GetMethod("Save", new[] { typeof(EntityOp?), typeof(bool) });
                if (saveMethod == null)
                    return BuildFailResult("实体未找到可用的Save方法", logs, success, fail);

                var userId = GetUserId();
                for (var i = 0; i < rows.Count; i++)
                {
                    var rowNo = i + 2;
                    var item = rows[i];

                    try
                    {
                        PrepareForInsert(entityType, item, userId);
                        saveMethod.Invoke(item, new object[] { null, false });
                        success++;
                        logs.Add($"第{rowNo}行导入成功");
                    }
                    catch (Exception ex)
                    {
                        fail++;
                        logs.Add($"第{rowNo}行导入失败: {GetInnermostMessage(ex)}");
                    }
                }

                var msg = fail == 0
                    ? $"导入完成，成功{success}条"
                    : $"导入完成，成功{success}条，失败{fail}条";

                // 导入审计：记录实体类型、文件名、成功/失败条数
                try
                {
                    string module = entityType.Namespace?.Contains(".DAL") == true ? entityType.Name : $"{entityType.Namespace}/{entityType.Name}";
                    App.Components.Logger.LogImport(module, file.FileName, success, fail);
                }
                catch { }

                return BuildResult(0, msg, new
                {
                    total = rows.Count,
                    success,
                    fail,
                    logs
                });
            }
            catch (Exception ex)
            {
                logs.Add($"导入失败: {GetInnermostMessage(ex)}");
                return BuildFailResult("导入失败", logs, success, fail);
            }
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

        /// <summary>创建Excel解析器</summary>
        /// <param name="entityType">实体类型</param>
        /// <param name="ignoreId">是否忽略ID</param>
        /// <returns>Excel解析器</returns>
        private static object CreateParser(Type entityType, bool ignoreId = false)
        {
            var parserType = typeof(ExcelParser<>).MakeGenericType(entityType);
            if (!ignoreId)
                return Activator.CreateInstance(parserType);

            Func<PropertyInfo, bool> filter = p => !IsIdLikeProperty(p?.Name);
            return Activator.CreateInstance(parserType, new object[] { filter });
        }

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

            if (!typeof(EntityBase).IsAssignableFrom(type))
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
            var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetCustomAttribute<NotMappedAttribute>() == null)
                .Where(p => (Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).IsBasicType())
                .Where(p => !ignoreId || !IsIdLikeProperty(p.Name))
                .ToList();

            foreach (var p in props)
            {
                // 过滤掉 CreatorId, OwnerId 等ID属性
                if (IsIdLikeProperty(p.Name))
                    continue;

                // 过滤掉只读属性
                var ui = p.GetCustomAttribute<UIAttribute>();
                if (ui != null && ui.ReadOnly)
                    continue;

                var title = string.IsNullOrWhiteSpace(ui?.Title) ? p.Name : ui.Title;
                headers.Add(string.Equals(title, p.Name, StringComparison.Ordinal) ? p.Name : $"{title}({p.Name})");
            }

            return headers;
        }

        /// <summary>判断属性是否为ID相关属性</summary>
        private static bool IsIdLikeProperty(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return false;

            return propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>准备插入数据（设置ID为0，设置CreatorId, OwnerId 等ID属性为当前用户ID）</summary>
        private static void PrepareForInsert(Type entityType, object item, long? userId)
        {
            // 导入场景默认按新增处理，避免误更新历史数据。
            var idProp = entityType.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (idProp != null && idProp.CanWrite && idProp.PropertyType == typeof(long))
                idProp.SetValue(item, 0L);

            if (userId != null)
            {
                // 设置 CreatorId, OwnerId 等ID属性为当前用户ID
                var creatorIdProp = entityType.GetProperty("CreatorId", BindingFlags.Public | BindingFlags.Instance);
                var ownerIdProp = entityType.GetProperty("OwnerId", BindingFlags.Public | BindingFlags.Instance);
                if (creatorIdProp != null && creatorIdProp.CanWrite && creatorIdProp.PropertyType == typeof(long?))
                {
                    var cur = creatorIdProp.GetValue(item) as long?;
                    if (cur == null)
                        creatorIdProp.SetValue(item, userId);
                }
                if (ownerIdProp != null && ownerIdProp.CanWrite && ownerIdProp.PropertyType == typeof(long?))
                {
                    var cur = ownerIdProp.GetValue(item) as long?;
                    if (cur == null)
                        ownerIdProp.SetValue(item, userId);
                }
            }
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
