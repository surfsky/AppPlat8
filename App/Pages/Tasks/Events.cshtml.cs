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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.EventView)]
    public class EventsModel : AdminModel
    {
        public Event Item { get; set; }
        public List<SelectListItem> EventTypes { get; set; }

        public void OnGet()
        {
            EventTypes = EventType.Set.Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList();
        }

        public async Task<IActionResult> OnGetData(Paging pi, string title, long? typeId, long? orgId, long? publisherId)
        {
            var list = Event.Search(title, typeId, orgId, publisherId).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.EventDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Event.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
