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
    [CheckPower(Power.CheckLogView)]
    public class CheckLogsModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public long? ObjectId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string ObjectName { get; set; }

        public Check Item { get; set; }

        public void OnGet(long? objectId, string objectName)
        {
            ObjectId = objectId;
            ObjectName = objectName;

            if (ObjectId.GetValueOrDefault() > 0 && string.IsNullOrWhiteSpace(ObjectName))
            {
                ObjectName = CheckObject.Get(ObjectId.Value)?.Name ?? string.Empty;
            }
        }

        public async Task<IActionResult> OnGetData(Paging pi, string objectName, string socialCreditCode, long? objectId, CheckObjectType? objectType, DateTime? checkStartDt, DateTime? checkEndDt)
        {
            var list = Check.Search(objectName, socialCreditCode, objectId, objectType, checkStartDt, checkEndDt).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.CheckLogDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Check.Delete(id);
            return BuildResult(0, "删除成功");
        }
    }
}
