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
        /// <summary>
        /// 导出Excel文件（通用泛型版本：默认中文扁平表头）。
        /// 自动过滤 virtual 属性（EF 导航属性）以及未重写 ToString() 的复杂类型属性，
        /// 避免导出 System.Collections.Generic.List`1[...] 之类的无意义内容。
        /// </summary>
        /// <param name="showTitleOrName">是否显示属性标题或名称（默认 true 显示标题）</param>
        /// <param name="excludeVirtual">是否排除 virtual 属性（置为 false 可保留 Id/创建时间 等标量 virtual 列）</param>
        /// <param name="objs">要导出的对象列表</param>
        /// <param name="fileName">导出的文件名（默认 "Export.xls"）</param>
        public static void Export<T>(IList<T> objs, string fileName = "Export.xls", bool showTitleOrName = true, bool excludeVirtual = true)
        {
            var response = Asp.Response;
            fileName = HttpUtility.UrlEncode(fileName, Encoding.UTF8);
            var bytes = ExcelHelper.ToExcelXml<T>(objs, showTitleOrName, excludeVirtual).ToBytes();
            response.ContentType = "application/vnd.ms-excel; charset=utf-8";
            response.Headers["Content-Disposition"] = "attachment;filename=" + fileName;
            response.Body.WriteAsync(bytes, 0, bytes.Length).GetAwaiter().GetResult();
        }

        /// <summary>导出 Excel（DataTable 版本：向后兼容）</summary>
        public static void Export(DataTable dt, string fileName = "Export.xls")
        {
            var response = Asp.Response;
            var bytes = ExcelHelper.ToExcelXml(dt).ToBytes();
            response.ContentType = "application/vnd.ms-excel; charset=utf-8";
            response.Headers["Content-Disposition"] = "attachment;filename=" + fileName;
            response.Body.WriteAsync(bytes, 0, bytes.Length).GetAwaiter().GetResult();
        }

        /// <summary>导出Excel文件（多层表头版本：传入 ExportColumnConfig，支持 EleColumnGroup 一致的层级合并）</summary>
        public static void Export(IEnumerable<object> objs, ExportColumnConfig cfg, string fileName = "Export.xls", bool showPropertyNameHeader = false, int fixRows = 1)
        {
            var response = Asp.Response;
            fileName = HttpUtility.UrlEncode(fileName, Encoding.UTF8);
            var bytes = ExcelHelper.ToExcelXml(objs, cfg, showPropertyNameHeader, fixRows).ToBytes();
            response.ContentType = "application/vnd.ms-excel; charset=utf-8";
            response.Headers["Content-Disposition"] = "attachment;filename=" + fileName;
            response.Body.WriteAsync(bytes, 0, bytes.Length).GetAwaiter().GetResult();
        }
    }
}
