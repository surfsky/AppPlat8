using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckObjectView)]
    public class ObjectContactsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        public CheckObjectContact Item { get; set; }

        public void OnGet(long objectId)
        {
            ObjectId = objectId;
            if (string.IsNullOrWhiteSpace(ObjectName))
            {
                ObjectName = CheckObject.Get(objectId)?.Name ?? string.Empty;
            }
        }

        public IActionResult OnGetData(Paging pi, long objectId, string name, string phone)
        {
            if (objectId <= 0)
                return BuildResult(400, "参数错误：缺少检查对象ID");

            var q = CheckObjectContact.IncludeSet.Where(o => o.CheckObject.Id == objectId);
            if (!string.IsNullOrWhiteSpace(name))
                q = q.Where(o => o.Name.Contains(name.Trim()));
            if (!string.IsNullOrWhiteSpace(phone))
                q = q.Where(o => o.Phone.Contains(phone.Trim()));

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

            var items = CheckObjectContact.IncludeSet
                .Where(o => ids.Contains(o.Id) && o.CheckObject.Id == objectId)
                .ToList();

            foreach (var item in items)
                item.Delete();

            return BuildResult(0, "删除成功");
        }
    }
}
