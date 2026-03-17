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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    using App.EleUI;
    using App.Entities;

    [CheckPower(Power.EventEdit)]
    public class EventFormModel : AdminModel
    {
        public Event Item { get; set; }
        public List<SelectListItem> EventTypes { get; set; }
        public List<Org> OrgTree { get; set; }

        public void OnGet()
        {
            EventTypes = EventType.Set.Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList();
            OrgTree = Org.GetTree();
        }

        public IActionResult OnGetData(long id)
        {
            var item = Event.GetDetail(id) ?? new Event();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] Event req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? Event.Get(req.Id) : new Event();
            if (item.Id == 0)
            {
                item.PublisherId = this.GetUserId();
            }
            item.Title = req.Title;
            item.TypeId = req.TypeId;
            item.TriggleDt = req.TriggleDt;
            item.OrgId = req.OrgId;
            item.Content = req.Content;
            item.MainImage = Uploader.SaveFile(nameof(Event),req.MainImage);
            item.AllowComment = req.AllowComment;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
