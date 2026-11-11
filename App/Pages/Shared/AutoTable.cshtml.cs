using System;
using System.Collections.Generic;
using System.IO;
using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    /// <summary>Excel 智能表格</summary>
    public class AutoTableModel : AuthModel
    {
        private readonly ExcelStore _store = new();

        [BindProperty(SupportsGet = true)]   public long AttId { get; set; }
        [BindProperty(SupportsGet = true)]   public string LabelPosition { get; set; } = "left";
        [BindProperty(SupportsGet = true)]   public int Shows { get; set; } = 4;

        /**打开页面 */
        public void OnGet()
        {
        }

        /**解析查询条件 */
        private static Dictionary<string, string> ParseFilters(IQueryCollection query)
        {
            const string prefix = "filter.";
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in query)
            {
                if (item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    item.Key.Length > prefix.Length && !string.IsNullOrWhiteSpace(item.Value.ToString()))
                    map[item.Key[prefix.Length..]] = item.Value.ToString();
            }
            return map;
        }

        /**读取表格元数据 */
        public IActionResult OnGetMeta(long attId)
        {
            try
            {
                var model = _store.ReadSheet(attId);
                return BuildResult(0, "ok", new
                {
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
        public IActionResult OnGetData(long attId, int pageIndex = 0, int pageSize = 20, string sortKey = "", string sortOrder = "")
        {
            try
            {
                var query = _store.Query(attId, ParseFilters(Request.Query), pageIndex, pageSize, sortKey, sortOrder);
                return BuildResult(0, "ok", new
                {
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
        public IActionResult OnGetDownload(long attId)
        {
            try
            {
                var (path, name) = _store.ResolveAttachment(attId);
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
                _store.DeleteRow(req.AttId, req.Id);
                return BuildResult(0, "删除成功");
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        public class AutoTableDeleteReq
        {
            public long AttId { get; set; }

            public int Id { get; set; }
        }

    }
}
