using App.Components;
using App.API;
using App.DAL;
using App.DAL.GIS;
using App.EleUI;
using Microsoft.AspNetCore.Mvc;
using System;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryEdit)]
    public class MenuFormModel : AdminModel
    {
        public GisMenu Item { get; set; }

        public void OnGet(long? id)
        {
        }

        public IActionResult OnGetData(long id, long? selectId)
        {
            var item = GisMenu.GetDetail(id) ?? new GisMenu();
            if (id == 0)
                item.ParentId = selectId;
            return BuildResult(0, "success", item.Export());
        }

        public IActionResult OnPostSave([FromBody] GisMenu req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            GisMenu item;
            if (req.Id > 0)
            {
                item = GisMenu.Get(req.Id);
                if (item == null)
                    return BuildResult(403, "无权编辑或数据不存在");
                if (req.ParentId == req.Id)
                    return BuildResult(400, "上级菜单不能是自己");
            }
            else
            {
                item = new GisMenu();
            }

            item.Name = req.Name;
            item.ParentId = req.ParentId;
            item.Icon = req.Icon;
            item.IsDefaultShow = req.IsDefaultShow;
            item.Zoom = req.Zoom;
            item.Selectable = req.Selectable;
            item.SortId = req.SortId;
            item.DataFrom = req.DataFrom;
            item.DataUrl = req.DataUrl?.Trim();
            item.DataCnt = req.DataCnt;
            item.DataDt = req.DataDt;
            item.Save();

            GisMenu.ClearCache();
            return BuildResult(0, "保存成功");
        }

        public IActionResult OnPostTestDataUrl([FromBody] GisMenu req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            try
            {
                var from = req.DataFrom ?? GisDataFrom.Geometry;
                if (from == GisDataFrom.API && string.IsNullOrWhiteSpace(req.DataUrl))
                    return BuildResult(400, "请先填写数据地址");

                var cnt = Gis.GetMenuGeometryCount(
                    menuId: req.Id > 0 ? req.Id : null,
                    dataFrom: from,
                    dataUrl: req.DataUrl?.Trim(),
                    menuName: req.Name?.Trim(),
                    icon: req.Icon);

                var now = DateTime.Now;
                var msg = $"测试成功，共 {cnt} 条数据";
                return EleManager
                    .SetControl<GisMenu>(t => t.DataCnt, Value: cnt)
                    .SetControl<GisMenu>(t => t.DataDt, Value: now.ToString("yyyy-MM-dd HH:mm:ss"))
                    .ToActionResult(msg);
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }
    }
}
