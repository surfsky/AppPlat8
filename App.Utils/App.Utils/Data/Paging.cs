using System;

namespace App.Components
{
    /// <summary>
    /// 分页及排序信息。
    /// </summary>
    public class Paging
    {
        public int PageSize { get; set; } = 50;                // 每页记录数
        public int PageIndex { get; set; } = 0;                // 当前页索引（0-based）
        public string SortField { get; set; }                  // 排序字段
        public string SortDirection { get; set; } = "ASC";      // 排序方向（ASC/DESC）

        // 附加信息
        public int Total { get; set; }       // 记录总数
        public int PageCount { get; set; }   // 页数

        /// <summary>
        /// 设置记录总数并计算页数。
        /// </summary>
        public Paging SetTotal(int total)
        {
            Total = total;
            var pageCount = Convert.ToInt32(Math.Ceiling((double)Total / Math.Max(1, PageSize)));
            PageCount = pageCount < 1 ? 1 : pageCount;
            return this;
        }
    }
}
