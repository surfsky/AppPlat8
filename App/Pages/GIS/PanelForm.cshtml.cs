using System;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
    public class PanelFormModel : AdminModel
    {
        public GisPanel Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(long id)
        {
            var item = GisPanel.GetDetail(id) ?? new GisPanel
            {
                InGis = true,
                InDashboard = true,
                Position = 0,
            };
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] GisPanel req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            GisPanel item;
            if (req.Id > 0)
            {
                item = GisPanel.Get(req.Id);
                if (item == null)
                    return BuildResult(403, "无权编辑或数据不存在");
            }
            else
            {
                item = new GisPanel();
                item.CreateDt = DateTime.Now;
                item.CreatorId = GetUserId();
            }

            item.Title = req.Title;
            item.Info = req.Info;
            item.Position = req.Position;
            item.Content = req.Content;
            item.ChartJson = req.ChartJson;
            item.InGis = req.InGis;
            item.InDashboard = req.InDashboard;

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
