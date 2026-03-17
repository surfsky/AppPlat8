using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.EleUI;
using App.DAL;
using App.Entities;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckTaskEdit)]
    public class CheckTaskFormModel : AdminModel
    {
        public CheckTask Item { get; set; }
        public List<Org> OrgTree { get; set; }

        public void OnGet()
        {
            OrgTree = Org.GetTree();
        }

        public IActionResult OnGetData(long id)
        {
            var item = CheckTask.GetDetail(id) ?? new CheckTask();
            return BuildResult(0, "success", item.Export(ExportMode.Normal));
        }

        public IActionResult OnPostSave([FromBody] CheckTask req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? CheckTask.Get(req.Id) : new CheckTask();
            if (item.Id == 0)
            {
                item.PublisherId = GetUserId();
            }

            item.Name = req.Name;
            item.ExpireDt = req.ExpireDt;
            item.Remark = req.Remark;
            //item.SetCheckObjectIds(req.CheckObjectIds);
            //item.SetOrgIds(req.OrgIds);
            //item.SetCheckSheetIds(req.CheckSheetIds);
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
