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
    /// <summary>事件清单（包括普通工作动态和突发事件）</summary>
    /// <param name="typeName">事件类别名称</param>
    public class EventsModel : AuthModel
    {
        public Event Item { get; set; } = new Event();
        public List<EventType> EventTypes { get; set; }
        public bool EventTypeEnabled { get; set; } = true;

        public void OnGet(string typeName = "")
        {
            EventTypes = EventType.GetTree();
            // 如果指定了 typeName 参数，默认选中该类别，且禁用类别选择器
            if (typeName.IsNotEmpty())
            {
                var type = EventType.Get(t => t.Name == typeName);
                if (type != null)
                {
                    Item.TypeId = type.Id;
                    EventTypeEnabled = false;
                }
            }
        }

        public IActionResult OnGetData(Paging pi, string title, long? typeId, string typeName, long? orgId, long? publisherId)
        {
            var cu = Auth.GetUser();
            var q = Event.Search(title, typeId, typeName, orgId, publisherId);
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
