using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Maintains
{
    [Auth(Power.FeedBackView)]
    public class FeedbacksModel : AdminModel
    {
        public Feedback Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(Paging pi, string user = null, string content = null,
            FeedType? type = null, FeedbackStatus? status = null, FeedApp? app = null, DateTime? createDt = null)
        {
            var list = Feedback.Search(
                    user: user,
                    keyword: content,
                    type: type,
                    status: status,
                    app: app,
                    fromDt: createDt)
                .SortPageExport(pi);

            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.FeedBackDelete))
                return BuildResult(403, "无权操作");

            var items = Feedback.Set.Where(t => ids.Contains(t.Id)).ToList();
            foreach (var item in items)
            {
                item.IsDel = true;
                item.Save();
            }

            return BuildResult(0, "删除成功");
        }
    }
}
