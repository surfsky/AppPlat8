using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    /// <summary>Excel 智能表格</summary>
    [Auth(Power.CheckObjectEdit)]
    public class AutoTableModel : AdminModel
    {
        private readonly AutoExcelStore _store = new();

        [BindProperty(SupportsGet = true)]
        public new string File { get; set; }

        [BindProperty(SupportsGet = true)]
        public string LabelPosition { get; set; } = "left";

        /**打开页面 */
        public void OnGet()
        {
        }

        /**读取表格元数据 */
        public IActionResult OnGetMeta(string file)
        {
            try
            {
                var model = _store.ReadSheet(file);
                return BuildResult(0, "ok", new
                {
                    file = model.File,
                    fileName = model.FileName,
                    sheetName = model.SheetName,
                    columns = model.Columns
                });
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        /**读取分页数据 */
        public IActionResult OnGetData(string file, string filters, int pageIndex = 0, int pageSize = 20, string sortKey = "", string sortOrder = "")
        {
            try
            {
                var query = _store.Query(file, ParseFilters(filters), pageIndex, pageSize, sortKey, sortOrder);
                return BuildResult(0, "ok", new
                {
                    file = query.File,
                    fileName = query.FileName,
                    sheetName = query.SheetName,
                    columns = query.Columns,
                    rows = query.Rows
                }, query.Pager);
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        /**下载原始 Excel */
        public IActionResult OnGetDownload(string file)
        {
            try
            {
                var path = _store.ResolveFilePath(file);
                var name = Path.GetFileName(path);
                var mime = IO.GetMimeType(Path.GetExtension(path));
                if (string.IsNullOrWhiteSpace(mime))
                    mime = "application/octet-stream";
                return PhysicalFile(path, mime, name);
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        /**删除数据行 */
        public IActionResult OnPostDelete([FromBody] AutoTableDeleteReq req)
        {
            try
            {
                _store.DeleteRow(req?.File, req?.Id ?? 0);
                return BuildResult(0, "删除成功");
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        public class AutoTableDeleteReq
        {
            public string File { get; set; }

            public int Id { get; set; }
        }

        /**解析查询条件 */
        private static Dictionary<string, string> ParseFilters(string filters)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(filters))
                return map;

            using var doc = JsonDocument.Parse(filters);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return map;

            foreach (var item in doc.RootElement.EnumerateObject())
                map[item.Name] = item.Value.ToString();
            return map;
        }
    }
}
