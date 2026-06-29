using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryView)]
    public class TyphoonsModel : AdminModel
    {
        public GisTyphoon Item { get; set; }

        /// <summary>初始化</summary>
        public void OnGet() {}

        /// <summary>获取列表</summary>
        public IActionResult OnGetData(Paging pi, int? year, string name, string code)
        {
            if (pi.SortField.IsEmpty())
            {
                pi.SortField = "Code";
                pi.SortDirection = "DESC";
            }
            var items = GisTyphoon.Search(name: name, code: code, year: year).SortPageExport(pi);
            return BuildResult(0, "success", items, pi);
        }
    }


}
