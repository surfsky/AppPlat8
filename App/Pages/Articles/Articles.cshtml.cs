using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.HttpApi;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.ArticleView)]
    public class ArticlesModel : AdminModel
    {
        public Article Item { get; set; }
        public List<ArticleDir> Categories { get; set; }

        public void OnGet()
        {
            this.Categories = ArticleDir.GetTree();
        }

        public async Task<IActionResult> OnGetData(Paging pi, string name, long? categoryId)
        {
            var list = Article.Search(name, categoryId).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.ArticleDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Article.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
