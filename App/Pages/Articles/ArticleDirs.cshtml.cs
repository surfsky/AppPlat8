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

namespace App.Pages.OA
{
    [CheckPower(Power.ArticleView)]
    public class ArticleDirsModel : AdminModel
    {
        public ArticleDir Item { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnGetData(Paging pi, string name)
        {
            var list = ArticleDir.GetTree();
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.ArticleDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                if (ArticleDir.Set.Any(x => x.ParentId == id))
                    return BuildResult(400, "存在下级目录，无法删除");
                if (Article.Set.Any(x => x.CategoryId == id))
                    return BuildResult(400, "该目录下存在文档，无法删除");

                ArticleDir.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }

    }
}
