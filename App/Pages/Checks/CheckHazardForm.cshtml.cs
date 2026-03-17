using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Checks
{
    [CheckPower(Power.CheckHazardEdit)]
    public class CheckHazardFormModel : AdminModel
    {
        public CheckHazard Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(long id)
        {
            var item = CheckHazard.GetDetail(id) ?? new CheckHazard();
            return BuildResult(0, "success", item.Export(ExportMode.Normal));
        }

        public IActionResult OnPostSave([FromBody] CheckHazard req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = CheckHazard.Get(req.Id);
            if (item == null)
            {
                // Typically hazards are created via API from mobile app or check process, not manually created here from scratch usually.
                // But for admin purpose, let's allow basic edit.
                return BuildResult(404, "隐患不存在");
            }

            item.Description = req.Description;
            item.Status = req.Status;
            item.ExpireDt = req.ExpireDt;
            item.RectifyDt = req.RectifyDt;
            item.IsIn141 = req.IsIn141;
            item.Save();

            // 保存图片
            item.AddAtt(Uploader.SaveFiles(nameof(CheckHazard), req.ImageUrls));
            return BuildResult(0, "保存成功");
        }

    }
}
