using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.ArticleEdit)]
    public class ArticleFormModel : AdminModel
    {
        public Article Item { get; set; }
        public List<ArticleDir> Categories { get; set; }

        public void OnGet()
        {
            Categories = ArticleDir.GetTree();
        }



        public IActionResult OnGetData(long id)
        {
            var item = Article.GetDetail(id) ?? new Article();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] Article req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = Article.Get(req.Id);
            if (item == null)
            {
                item = new Article();
                item.CreateDt = DateTime.Now;
            }

            item.Name = req.Name;
            item.CategoryId = req.CategoryId;
            item.Content = req.Content;
            item.Attachments = req.Attachments;
            item.AllowComment = req.AllowComment;

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
