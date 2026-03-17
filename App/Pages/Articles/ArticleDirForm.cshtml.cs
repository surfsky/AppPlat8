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
using App.EleUI;

namespace App.Pages.OA
{
    [CheckPower(Power.ArticleEdit)]
    public class ArticleDirFormModel : AdminModel
    {
        public ArticleDir Item { get; set; }
        public List<ArticleDir> CategoryTree { get; set; }

        public void OnGet(long? parentId)
        {
            this.CategoryTree = ArticleDir.GetTree();

            //if (Item == null) Item = new ArticleCategory();
            //if (parentId.HasValue && parentId != 0) Item.ParentId = parentId.Value;
        }

        public IActionResult OnGetData(long id, long? selectId)
        {
            var item = ArticleDir.GetDetail(id);
            if (item == null)
            {
                item = new ArticleDir();
                item.ParentId = selectId.Value;
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] ArticleDir req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? ArticleDir.Get(req.Id) : new ArticleDir();
            item.Name = req.Name;
            item.SortId = req.SortId;
            item.ParentId = req.ParentId;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
