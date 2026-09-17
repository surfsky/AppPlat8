using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using App.Utils;
using App.Web;

namespace App.Web
{
    /// <summary>
    /// Excel 操作辅助类
    /// </summary>
    public class ExcelExporter
    {
        /// <summary>导出Excel文件（通用泛型版本：默认中文扁平表头）</summary>
        public static void Export<T>(IList<T> objs, string fileName = "Export.xls", bool showFieldDescription=false)
        {
            var response = Asp.Response;
            fileName = HttpUtility.UrlEncode(fileName, Encoding.UTF8);
            var bytes = ExcelHelper.ToExcelXml<T>(objs, showFieldDescription).ToBytes();
            response.ContentType = "application/vnd.ms-excel; charset=utf-8";
            response.Headers["Content-Disposition"] = "attachment;filename=" + fileName;
            response.Body.WriteAsync(bytes, 0, bytes.Length).GetAwaiter().GetResult();
        }

        // 导出 Excel（DataTable 版本：向后兼容）
        public static void Export(DataTable dt, string fileName = "Export.xls")
        {
            var response = Asp.Response;
            var bytes = ExcelHelper.ToExcelXml(dt).ToBytes();
            response.ContentType = "application/vnd.ms-excel; charset=utf-8";
            response.Headers["Content-Disposition"] = "attachment;filename=" + fileName;
            response.Body.WriteAsync(bytes, 0, bytes.Length).GetAwaiter().GetResult();
        }

        /// <summary>导出Excel文件（多层表头版本：传入 ExportColumnConfig，支持 EleColumnGroup 一致的层级合并）</summary>
        public static void Export(IEnumerable<object> objs, ExportColumnConfig cfg, string fileName = "Export.xls", bool showPropertyNameHeader = false)
        {
            var response = Asp.Response;
            fileName = HttpUtility.UrlEncode(fileName, Encoding.UTF8);
            var bytes = ExcelHelper.ToExcelXml(objs, cfg, showPropertyNameHeader).ToBytes();
            response.ContentType = "application/vnd.ms-excel; charset=utf-8";
            response.Headers["Content-Disposition"] = "attachment;filename=" + fileName;
            response.Body.WriteAsync(bytes, 0, bytes.Length).GetAwaiter().GetResult();
        }
    }
}