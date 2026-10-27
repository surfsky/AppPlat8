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

namespace App.Pages.Events
{
    [Auth(Power.EventView)]
    public class EventTypesModel : AuthModel
    {
        public EventType Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(Paging pi, string name)
        {
            var list = EventType.GetTree();
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)      return BuildResult(400, "参数错误");
            if (!HasPower(Power.EventDelete))        return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                if (EventType.Set.Any(x => x.ParentId == id))  return BuildResult(400, "存在下级类别，无法删除");
                if (Event.Set.Any(x => x.TypeId == id))            return BuildResult(400, "该类别下存在事件，无法删除");
                EventType.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }
    }
}
