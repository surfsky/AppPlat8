using System;

namespace App.Components
{
    /// <summary>
    /// 分页及排序信息。
    /// </summary>
    public class Paging
    {
        // 请求信息
        public int PageSize { get; set; } = 50;
        public int PageIndex { get; set; } = 0;
        public string SortField { get; set; }
        public string SortDirection { get; set; } = "ASC";

        // 附加返回信息
        public int Total { get; set; }
        public int PageCount { get; set; }

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
