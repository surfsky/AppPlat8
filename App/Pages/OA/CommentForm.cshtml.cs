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
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.CommentEdit)]
    public class CommentFormModel : AdminModel
    {
        public Comment Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id)
        {
            var item = Comment.GetDetail(id) ?? new Comment();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] Comment req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? Comment.Get(req.Id) : new Comment();
            item.TargetId = req.TargetId;
            item.Type = req.Type;
            item.Author = req.Author;
            item.Rating = req.Rating;
            item.Content = req.Content;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
