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
    [CheckPower(Power.GisGeometryEdit)]
    public class GeometryFormModel : AdminModel
    {
        public GisGeometry Item { get; set; }
        public List<App.DAL.Org> OrgTree { get; set; }

        public void OnGet()
        {
            OrgTree = App.DAL.Org.GetTree();
        }

        public IActionResult OnGetData(long id)
        {
            var item = GisGeometry.GetDetail(id) ?? new GisGeometry();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] GisGeometry req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = GisGeometry.Get(req.Id);
            if (item == null)
            {
                item = new GisGeometry();
                item.CreateDt = DateTime.Now;
                item.CreatorId = GetUserId();
            }

            item.Name = req.Name;
            item.Alias = req.Alias;
            item.ParentId = req.ParentId;
            item.OrgId = req.OrgId;
            item.JsonData = req.JsonData;

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
