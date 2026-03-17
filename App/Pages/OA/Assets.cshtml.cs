using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace App.Pages.OA
{
    [CheckPower(Power.AssetView)]
    public class AssetsModel : AdminModel
    {
        public Asset Item { get; set; }

        public void OnGet(){}

        public async Task<IActionResult> OnGetData(Paging pi, string name, AssetCategory? category)
        {
            var list = Asset.Search(name, category, null).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody]long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.AssetDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Asset.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
