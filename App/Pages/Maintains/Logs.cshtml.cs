using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using App.EleUI;
using App.DAL;
using App.Utils;
using App.Components;
using App.Web;
using App.HttpApi;
using App.Entities;

namespace App.Pages.Maintains
{
    public class LogsModel : AdminModel
    {
        public Log Item {get; set; }

        public void OnGet(){}

        public async Task<IActionResult> OnGetData(Paging pi, string name, string message, LogLevel? level, DateTime? startDt, string ip)
        {
            var list = Log.Search(name, message, level, startDt, ip).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete()
        {
            int n = Log.DeleteBatch();
            return BuildResult(0, $"删除成功{n}条记录");
        }
    }
}
