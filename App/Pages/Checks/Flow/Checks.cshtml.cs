using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [Auth(Permission.CheckView)]
    public class ChecksModel : AuthModel
    {
        public long? ObjectId { get; set; }
        public string ObjectName { get; set; }
        public Check Item { get; set; } = new Check();

        public void OnGet(long? objectId, string objectName, long? orgId)
        {
            ObjectId = objectId;
            ObjectName = objectName;
            if (ObjectId.GetValueOrDefault() > 0 && string.IsNullOrWhiteSpace(ObjectName))
            {
                ObjectName = CheckObject.Get(ObjectId)?.Name ?? string.Empty;
            }
            Item.OrgId = orgId;
        }

        public IActionResult OnGetData(Paging pi, string objectName, string socialCreditCode, long? objectId, CheckObjectType? objectType, DateTime? checkStartDt, DateTime? checkEndDt, long? orgId=null)
        {
            var q = Check.Search(objectName, socialCreditCode, objectId, objectType, checkStartDt, checkEndDt, orgId);
            q = q.FilterByOrg(Auth.GetUser());
            var list = q.SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!HasPermission(Permission.CheckDelete))
                return BuildResult(403, "无权操作");

            var cu = Auth.GetUser();
            foreach (var id in ids)
            {
                var target = Check.Get(id);
                if (target == null) continue;
                if (!OrgFilter.IsAuth(cu, target.OrgId))
                    return BuildResult(403, $"无权删除记录（Id={id}）");
                Check.Delete(id);
            }
            return BuildResult(0, "删除成功");
        }
    }
}
