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
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.BudgetEdit)]
    public class BudgetTypeFormModel : AdminModel
    {
        public BudgetType Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id)
        {
            var item = BudgetType.GetDetail(id) ?? new BudgetType();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] BudgetType req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? BudgetType.Get(req.Id) : new BudgetType();
            item.Name = req.Name;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
