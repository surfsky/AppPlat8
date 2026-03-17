using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace App.Utils
{
    /// <summary>
    /// Excel 操作辅助类
    /// </summary>
    public class ExcelHelper
    {
        // 输出 Excel Xml
        public static string ToExcelXml<T>(IList<T> objs, bool showFieldDescription=false)
        {
            if (objs.IsEmpty())
                return "";

            //var type = typeof(T);
            var type = objs[0].GetType();
            var attrs = new UISetting(type).Items;
            var props = type.GetProperties();

            // 表开始
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            sb.AppendLine(" <Worksheet ss:Name=\"Sheet1\">");
            sb.AppendLine("  <Table>");

            // 输出字段名
            sb.AppendLine("   <Row>");
            foreach (var prop in props)
                sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + prop.Name + "</Data></Cell>");
            sb.AppendLine("   </Row>");

            // 输出标题
            if (showFieldDescription)
            {
                sb.AppendLine("   <Row>");
                foreach (var attr in attrs)
                    sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + attr.ToString() + "</Data></Cell>");
                sb.AppendLine("   </Row>");
            }


            // 输出数据
            foreach (var obj in objs)
            {
                sb.AppendLine("   <Row>");
                foreach (var prop in props)
                {
                    var val = obj.GetValue(prop.Name).ToText();
                    sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + val.Replace("<", "＜") + "</Data></Cell>");
                }
                sb.AppendLine("   </Row>");
            }

            // 表结束
            sb.AppendLine("  </Table>");
            sb.AppendLine(" </Worksheet>");
            sb.AppendLine("</Workbook>");
            return sb.ToString();
        }

        // 将 DataTable 转化为 ExcelXml
        public static string ToExcelXml(DataTable dt)
        {
            // 表开始
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\"?>");
            sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
            sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
            sb.AppendLine(" <Worksheet ss:Name=\"Sheet1\">");
            sb.AppendLine("  <Table>");

            // 输出标题和数据
            sb.AppendLine("   <Row>");
            for (int i = 0; i < dt.Columns.Count; i++)
                sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + dt.Columns[i].Caption + "</Data></Cell>");
            sb.AppendLine("   </Row>");

            //输出所有列数据
            foreach (DataRow dr in dt.Rows)
            {
                sb.AppendLine("   <Row>");
                Object[] ary = dr.ItemArray;
                for (int i = 0; i <= ary.GetUpperBound(0); i++)
                    sb.AppendLine("    <Cell><Data ss:Type=\"String\">" + ary[i].ToString().Replace("<", "＜") + "</Data></Cell>");
                sb.AppendLine("   </Row>");
            }

            // 表结束
            sb.AppendLine("  </Table>");
            sb.AppendLine(" </Worksheet>");
            sb.AppendLine("</Workbook>");
            return sb.ToString();
        }
    }
}