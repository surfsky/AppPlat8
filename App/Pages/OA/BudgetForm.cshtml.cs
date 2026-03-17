using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.BudgetEdit)]
    public class BudgetFormModel : AdminModel
    {
        public Budget Item { get; set; }
        public List<SelectListItem> BudgetTypes { get; set; }

        public void OnGet()
        {
            BudgetTypes = BudgetType.Set.Select(t => new SelectListItem(t.Name, t.Id.ToString())).ToList();
        }

        public IActionResult OnGetData(long id)
        {
            var item = Budget.GetDetail(id) ?? new Budget();
            if (item.Id == 0)
            {
                item.Year = DateTime.Now.Year;
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] Budget req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? Budget.Get(req.Id) : new Budget();
            item.Name = req.Name;
            item.Year = req.Year;
            item.TypeId = req.TypeId;
            item.OrgId = req.OrgId;
            item.Company = req.Company;
            item.Project = req.Project;
            item.PayDt = req.PayDt;
            item.PayStatus = req.PayStatus;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
