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
    [CheckPower(Power.BudgetView)]
    public class BudgetsModel : AdminModel
    {
        public Budget Item { get; set; }
        public List<SelectListItem> BudgetTypes { get; set; }

        public void OnGet()
        {
            BudgetTypes = BudgetType.Set.Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList();
        }

        public async Task<IActionResult> OnGetData(Paging pi, int? year, long? orgId, long? typeId, string name, bool includeChildOrgs = false)
        {
            var list = Budget.Search(name, year, orgId, typeId, null, includeChildOrgs).SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.BudgetDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
                Budget.Delete(id);
            return BuildResult(0, "删除成功");
        }

        public IActionResult OnPostSave([FromBody] Budget req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? Budget.Get(req.Id) : new Budget();
            item.Name = req.Name;
            item.Year = req.Year;
            item.OrgId = req.OrgId;
            item.TypeId = req.TypeId;
            item.Amount = req.Amount;
            item.Remark = req.Remark;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
