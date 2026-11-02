using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.EleUI;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NuGet.Common;

namespace App.Pages.Events
{
    //[Auth(Power.EventView)]
    public class EventsModel : AuthModel
    {
        public Event Item { get; set; }
        public List<EventType> EventTypes { get; set; }

        public void OnGet()
        {
            EventTypes = EventType.GetTree();
        }

        public IActionResult OnGetData(Paging pi, string title, long? typeId, long? orgId, long? publisherId)
        {
            var cu = Auth.GetUser();
            var q = Event.Search(title, typeId, orgId, publisherId);
            q = q.FilterByOrg(cu);
            var list = q.SortPageExport(pi);
            //EleManager.ShowToast("定时刷新", NotifyType.Info);
            Console.WriteLine("定时刷新" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!HasPower(Power.EventDelete))
                return BuildResult(403, "无权操作");

            var cu = Auth.GetUser();
            var isAdmin = Auth.IsAdmin(cu);
            var scopeId = cu?.AuthOrgId;
            foreach (var id in ids)
            {
                var target = Event.Get(id);
                if (target == null) continue;
                if (!OrgFilter.IsAuth(cu, target.OrgId))
                    return BuildResult(403, $"无权删除记录（Id={id}）");
                Event.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }
    }
}
