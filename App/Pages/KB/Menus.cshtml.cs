using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.HttpApi;
using App.Utils;
using App.Entities; // Added
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.KB
{
    [Auth(Power.KbMenuView)]
    public class MenusModel : AdminModel
    {
        public KbMenu Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(Paging pi, string name)
        {
            var list = KbMenu.GetTree();
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.KbMenuDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                if (KbMenu.Set.Any(x => x.ParentId == id))
                    return BuildResult(400, "存在下级目录，无法删除");

                KbMenu.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }

    }
}
