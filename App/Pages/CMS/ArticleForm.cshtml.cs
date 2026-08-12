using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.EleUI;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [Auth(Power.ArticleEdit)]
    public class ArticleFormModel : AdminModel
    {
        public Article Item { get; set; }
        public List<ArticleMenu> Categories { get; set; }

        public void OnGet()
        {
            Categories = ArticleMenu.GetTree();
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
            item.MenuId = req.MenuId;
            item.Content = req.Content;
            item.AllowComment = req.AllowComment;

            item.Save();
            return BuildResult(0, "保存成功");
        }

    }
}
