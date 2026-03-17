using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckSheetEdit)]
    public class CheckSheetFormModel : AdminModel
    {
        public CheckSheet Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id)
        {
            var item = CheckSheet.GetDetail(id) ?? new CheckSheet();
            return BuildResult(0, "success", item.Export(ExportMode.Normal));
        }

        public IActionResult OnPostSave([FromBody] CheckSheet req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = CheckSheet.Get(req.Id);
            if (item == null)
            {
                item = new CheckSheet();
                item.CreateDt = DateTime.Now;
            }

            item.Name = req.Name;
            item.Scope = req.Scope;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
