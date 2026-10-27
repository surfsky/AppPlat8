using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Events
{
    using App.EleUI;
    using App.Entities;

    [Auth(Power.EventEdit)]
    public class EventFormModel : AuthModel
    {
        public Event Item { get; set; }
        public List<EventType> EventTypes { get; set; }
        public List<App.DAL.Org> OrgTree { get; set; }

        public void OnGet()
        {
            EventTypes = EventType.GetTree();
            OrgTree = App.DAL.Org.GetTree();
        }

        public IActionResult OnGetData(long id)
        {
            var item = Event.GetDetail(id) ?? new Event();
            if (id > 0 && item != null && item.Id > 0)
            {
                var cu = Auth.GetUser();
                if (!OrgFilter.IsAuth(cu, item.OrgId))
                    return BuildResult(403, "越权访问");
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] Event req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? Event.Get(req.Id) : new Event();
            // 非 admin 修改已有记录先做范围校验
            if (req.Id > 0)
            {
                var cu = Auth.GetUser();
                if (!OrgFilter.IsAuth(cu, item?.OrgId))
                    return BuildResult(403, "越权修改");
            }
            if (item.Id == 0)
            {
                item.PublisherId = this.GetUserId();
            }
            item.Title = req.Title;
            item.TypeId = req.TypeId;
            item.HappenDt = req.HappenDt;
            item.OrgId = req.OrgId;
            item.Content = req.Content;
            item.MainImage = Uploader.SaveFile(nameof(Event),req.MainImage);
            item.AllowComment = req.AllowComment;
            item.Gps = req.Gps;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
