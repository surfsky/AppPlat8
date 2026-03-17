using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using App.HttpApi;

namespace App.Pages.Admin
{
    [Auth(Power.AnnounceView)]
    //[CheckPower(Power.MonitorMessage)]
    [IgnoreAntiforgeryToken]
    public class AnnounceFormModel : AdminModel
    {
        [BindProperty]
        public Announce Item { get; set; }

        public void OnGet(){}

        /// <summary>获取公告详情</summary>
        public IActionResult OnGetData(long id)
        {
            if (id == 0)
            {
                if (!CheckPower(Power.AnnounceNew))
                    return BuildResult(403, "无权新增");
                var a = new Announce
                {
                    Title = "",
                    Author = GetUser()?.RealName,
                    AuthorId = GetUserId(),
                    Status = AnnounceStatus.Draft,
                    CreateTime = DateTime.Now,
                    PublishTime = null,
                    Priority = null,
                    ViewCount = null,
                    IsTop = false,
                    Summary = "",
                    Content = ""
                };
                return BuildResult(0, "success", a);
            }
            else
            {
                if (!CheckPower(Power.AnnounceView))
                    return BuildResult(403, "无权查看");
                var a = Announce.Get(id);
                if (a == null)
                    return BuildResult(404, "无效参数");
                return BuildResult(0, "success", a);
            }
        }

        /// <summary>保存公告</summary>
        public IActionResult OnPostSave([FromBody] Announce req)
        {
            if (req == null || String.IsNullOrWhiteSpace(req.Title))
                return BuildResult(400, "标题不能为空");
            var needPower = (req.Id == 0) ? Power.AnnounceNew : Power.AnnounceEdit;
            if (!CheckPower(needPower))
                return BuildResult(403, "无权操作");

            var userName = GetUserName();
            var curUser = App.DAL.User.Set.FirstOrDefault(u => u.Name == userName);

            Announce a = req.Id == 0 ? new Announce() : Announce.Get(req.Id);
            a.Title = req.Title.Trim();
            a.Author = req.Author;
            a.AuthorId = curUser?.Id;
            a.Content = req.Content;
            a.Summary = req.Summary;
            a.Status = req.Status;
            a.Priority = req.Priority;
            a.ViewCount = req.ViewCount;
            a.IsTop = req.IsTop;
            a.PublishTime = req.PublishTime;
            a.CreateTime = (req.CreateTime ?? a.CreateTime) ?? DateTime.Now;

            // 已发布但未设置发布时间时自动补齐
            if (a.Status == AnnounceStatus.Published && !a.PublishTime.HasValue)
                a.PublishTime = DateTime.Now;
            a.Save(null, log: true);
            return BuildResult(0, "保存成功", new { id = a.Id });
        }
    }
}
