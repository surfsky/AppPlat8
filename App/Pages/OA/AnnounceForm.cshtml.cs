using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using App.HttpApi;

namespace App.Pages.Admin
{
    [Auth(Power.AnnounceView)]
    public class AnnounceFormModel : AuthModel
    {
        [BindProperty]
        public Announce Item { get; set; }

        public void OnGet(){}

        /// <summary>获取公告详情</summary>
        public IActionResult OnGetData(long id)
        {
            var user = GetUser();
            if (id == 0)
            {
                if (!HasPower(Power.AnnounceNew))
                    return BuildResult(403, "无权新增");
                var a = new Announce
                {
                    Title = "",
                    Author = user.RealName,
                    AuthorId = user.Id,
                    OrgId = user.OrgId,
                    Status = AnnounceStatus.Draft,
                    CreateDt = DateTime.Now,
                    PublishDt = null,
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
                var a = Announce.Get(id);
                return BuildResult(0, "success", a);
            }
        }

        /// <summary>保存公告</summary>
        public IActionResult OnPostSave([FromBody] Announce req)
        {
            var user = Auth.GetUser();
            if (req == null || String.IsNullOrWhiteSpace(req.Title))
                return BuildResult(400, "标题不能为空");
            var needPower = (req.Id == 0) ? Power.AnnounceNew : Power.AnnounceEdit;
            if (!HasPower(needPower))
                return BuildResult(403, "无权操作");


            Announce a = req.Id == 0 ? new Announce() : Announce.Get(req.Id);
            a.Title = req.Title.Trim();
            a.Author = req.Author;
            a.AuthorId = user.Id;
            a.Content = req.Content;
            a.Summary = req.Summary;
            a.Status = req.Status;
            a.Priority = req.Priority;
            a.ViewCount = req.ViewCount;
            a.IsTop = req.IsTop;
            a.PublishDt = req.PublishDt;
            a.OrgId = user.OrgId;

            // 已发布但未设置发布时间时自动补齐
            if (a.Status == AnnounceStatus.Published && !a.PublishDt.HasValue)
                a.PublishDt = DateTime.Now;
            a.Save();
            return BuildResult(0, "保存成功", new { id = a.Id });
        }
    }
}
