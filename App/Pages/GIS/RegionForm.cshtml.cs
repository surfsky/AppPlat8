using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisRegionEdit)]
    public class RegionFormModel : AdminModel
    {
        public GisRegion Item { get; set; }
        public List<Org> OrgTree { get; set; }

        public void OnGet()
        {
            OrgTree = Org.GetTree();
        }

        public IActionResult OnGetData(long id)
        {
            var item = GisRegion.GetDetail(id) ?? new GisRegion();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] GisRegion req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = GisRegion.Get(req.Id);
            if (item == null)
            {
                item = new GisRegion();
                item.CreateDt = DateTime.Now;
            }

            item.Name = req.Name;
            item.Alias = req.Alias;
            item.RegionType = req.RegionType;
            item.OrgId = req.OrgId;
            item.Creator = req.Creator;
            item.JsonData = req.JsonData;

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
