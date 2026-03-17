using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.CompanyView)]
    public class CompaniesModel : AdminModel
    {
        [BindProperty]
        public Company Item { get; set; }

        public void OnGet(){}

        /// <summary>查询厂商列表</summary>
        public async Task<IActionResult> OnGetData(Paging pi, string name, string unifiedSocialCreditCode)
        {
            var list = Company.Search(name, null, null).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        /// <summary>删除厂商</summary>
        public IActionResult OnPostDelete([FromBody]long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CompanyDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Company.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
