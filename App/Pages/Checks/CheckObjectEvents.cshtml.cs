using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckObjectView)]
    public class CheckObjectEventsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        public CheckObjectEvent Item { get; set; }
        public List<SelectListItem> TypeItems { get; set; }

        public void OnGet(long objectId)
        {
            ObjectId = objectId;
            if (string.IsNullOrWhiteSpace(ObjectName))
                ObjectName = CheckObject.Get(objectId)?.Name ?? string.Empty;

            TypeItems = BuildTypeItems();
        }

        public IActionResult OnGetData(Paging pi, long objectId, string title, CheckObjectEventType? type)
        {
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");

            var q = CheckObjectEvent.Search(objectId, title, type);
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids, long objectId)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");
            if (!CheckPower(Power.CheckObjectEdit))
                return BuildResult(403, "无权操作");

            var items = CheckObjectEvent.IncludeSet
                .Where(o => ids.Contains(o.Id) && o.CheckObjectId == objectId)
                .ToList();

            foreach (var item in items)
                item.Delete();

            return BuildResult(0, "删除成功");
        }

        private static List<SelectListItem> BuildTypeItems()
        {
            return Enum.GetValues<CheckObjectEventType>()
                .Select(t => new SelectListItem(t.GetTitle(), ((int)t).ToString()))
                .ToList();
        }
    }
}
