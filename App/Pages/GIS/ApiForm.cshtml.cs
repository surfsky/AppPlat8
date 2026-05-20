using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
    public class ApiFormModel : AdminModel
    {
        public GisApi Item { get; set; }

        public void OnGet()
        {
        }

        public IActionResult OnGetData(long id, long? menuId)
        {
            var item = GisApi.GetDetail(id) ?? new GisApi();
            if (id == 0 && menuId.IsNotEmpty())
                item.MenuId = menuId;
            return BuildResult(0, "success", item.Export());
        }

        public IActionResult OnPostSave([FromBody] GisApi req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            GisApi item;
            if (req.Id > 0)
            {
                item = GisApi.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "对象不存在");
            }
            else
            {
                item = new GisApi();
            }

            item.MenuId = req.MenuId;
            item.Name = req.Name?.Trim();
            item.DataUrl = req.DataUrl?.Trim();
            item.IsEnabled = req.IsEnabled;
            item.SortId = req.SortId;
            item.DataCnt = req.DataCnt;
            item.DataDt = req.DataDt;
            item.LastErr = req.LastErr;
            item.Save();

            return BuildResult(0, "保存成功", item.Export());
        }
    }
}
