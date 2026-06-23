using System;
using System.Collections.Generic;
using System.Text.Json;
using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    /// <summary>Excel 智能表单</summary>
    [CheckPower(Power.CheckObjectEdit)]
    public class AutoFormModel : AdminModel
    {
        private readonly AutoExcelStore _store = new();

        [BindProperty(SupportsGet = true)]
        public new string File { get; set; }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; }

        /**打开页面 */
        public void OnGet()
        {
        }

        /**读取表单元数据 */
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

        /**读取单行 */
        public IActionResult OnGetRow(string file, int id)
        {
            try
            {
                var row = _store.GetRow(file, id);
                if (row == null)
                    return BuildResult(404, "数据不存在");

                return BuildResult(0, "ok", row);
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        /**保存数据 */
        public IActionResult OnPostSave([FromBody] AutoFormSaveReq req)
        {
            try
            {
                var vals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in req?.Values ?? new Dictionary<string, object>())
                {
                    vals[item.Key] = GetReqValue(item.Value);
                }

                var row = _store.SaveRow(req?.File, req?.Id > 0 ? req.Id : null, vals);
                return BuildResult(0, "保存成功", row);
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        /**解析提交值 */
        private static string GetReqValue(object val)
        {
            if (val == null)
                return string.Empty;

            if (val is JsonElement json)
            {
                return json.ValueKind switch
                {
                    JsonValueKind.Null => string.Empty,
                    JsonValueKind.String => json.GetString() ?? string.Empty,
                    JsonValueKind.Number => json.ToString(),
                    JsonValueKind.True => bool.TrueString,
                    JsonValueKind.False => bool.FalseString,
                    JsonValueKind.Object when json.TryGetProperty("value", out var txt) => txt.ToString(),
                    _ => json.ToString()
                };
            }

            return Convert.ToString(val) ?? string.Empty;
        }

        public class AutoFormSaveReq
        {
            public string File { get; set; }

            public int? Id { get; set; }

            public Dictionary<string, object> Values { get; set; }
        }
    }
}
