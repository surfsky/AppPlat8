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
        /// <summary>导出Excel文件</summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objs"></param>
        /// <param name="fileName"></param>
        /// <param name="showFieldDescription"></param>
        public static void Export<T>(IList<T> objs, string fileName = "Export.xls", bool showFieldDescription=false)
        {
            var response = Asp.Response;
            fileName = HttpUtility.UrlEncode(fileName, Encoding.UTF8);
            var bytes = ExcelHelper.ToExcelXml<T>(objs, showFieldDescription).ToBytes(); // 还是用xml吧，每个字段都是字符串类型，避免客户输入不同格式的数据
            //response.ClearContent();
            //response.ContentEncoding = Encoding.UTF8;
            response.ContentType = "application/vnd.ms-excel; charset=utf-8";
            response.Headers["Content-Disposition"] = "attachment;filename=" + fileName;
            response.Body.Write(bytes, 0, bytes.Length); 
            //response.End();
        }

        // 导出Excel文件
        public static void Export(DataTable dt, string fileName = "Export.xls")
        {
            var response = Asp.Response;
            //fileName = HttpUtility.UrlEncode(fileName, Encoding.UTF8);
            var bytes = ExcelHelper.ToExcelXml(dt).ToBytes();
            //response.ClearContent();
            //response.ContentEncoding = Encoding.UTF8;
            response.ContentType = "application/vnd.ms-excel; charset=utf-8";
            response.Headers["Content-Disposition"] = "attachment;filename=" + fileName;
            response.Body.Write(bytes, 0, bytes.Length); // 还是用xml吧，每个字段都是字符串类型，避免客户输入不同格式的数据
            //response.End();
        }

    }
}