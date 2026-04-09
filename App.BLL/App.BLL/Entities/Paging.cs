using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using App.DAL;

namespace App.Components
{
    /// <summary>分页及排序信息</summary>
    public class Paging
    {
        // 请求信息
        public int PageSize { get; set; } = 50;
        public int PageIndex { get; set; } = 0;
        public string SortField { get; set; }
        public string SortDirection { get; set; } = "ASC";

        // 附加的返回信息
        public int Total { get; set; }      // 记录总数
        public int PageCount { get; set; }  // 分页数量

        //
        public Paging() { }
        public Paging(string sortField, bool sortDirection)
        {
            SortField = sortField;
            SortDirection = sortDirection ? "ASC" : "DESC";
            PageIndex = 0;
            PageSize = SiteConfig.Instance.PageSize;
        }
        public Paging(string sortField, string sortDirection)
        {
            SortField = sortField;
            SortDirection = sortDirection;
            PageIndex = 0;
            PageSize = SiteConfig.Instance.PageSize;
        }

        /// <summary>设置记录总数（并设置好页数）</summary>
        public Paging SetTotal(int total)
        {
            this.Total = total;
            int pageCount = Convert.ToInt32(Math.Ceiling((double)Total / (double)PageSize));
            this.PageCount = pageCount < 1 ? 1 : pageCount;
            return this;
        }
    }
}