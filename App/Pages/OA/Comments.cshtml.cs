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

namespace App.Pages.OA
{
    [CheckPower(Power.CommentView)]
    public class CommentsModel : AdminModel
    {
        public Comment Item { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnGetData(Paging pi, long? targetId, CommentType? type, string author)
        {
            var list = Comment.Search(targetId, type, author).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CommentDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Comment.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
