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
    [Auth(Power.CommentView)]
    public class CommentsModel : AuthModel
    {
        public Comment Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(Paging pi, long? targetId, CommentType? type, string author)
        {
            var cu = Auth.GetUser();
            var q = Comment.Search(targetId, type, author);
            if (!Auth.IsAdmin(cu))
                q = q.FilterByOrg(cu);
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CommentDelete))
                return BuildResult(403, "无权操作");

            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);
            var scopeId = cu?.EffectiveAuthOrgId;
            foreach (var id in ids)
            {
                var target = Comment.Get(id);
                if (target == null) continue;
                if (!isAdmin && !OrgFilter.IsAuth(scopeId, target.OrgId))
                    return BuildResult(403, $"无权删除记录（Id={id}）");
                Comment.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }
    }
}
