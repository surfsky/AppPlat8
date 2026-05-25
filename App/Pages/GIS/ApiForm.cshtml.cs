using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.EleUI;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using System;

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
            item.IsLive = req.IsLive;
            item.SortId = req.SortId;
            item.DataCnt = req.DataCnt;
            item.DataDt = req.DataDt;
            item.LastErr = req.LastErr;
            item.Save();

            return BuildResult(0, "保存成功", item.Export());
        }

        public IActionResult OnPostTestApi([FromBody] GisApi req)
        {
            var apiId = req?.Id ?? 0;
            var dataUrl = req?.DataUrl?.Trim() ?? string.Empty;

            if (apiId <= 0 && string.IsNullOrWhiteSpace(dataUrl))
                return EleManager.ShowNotify("请先填写接口地址", NotifyType.Warning, "提示");

            var url = apiId > 0
                ? $"/GIS/ApiData?apiId={apiId}"
                : $"/GIS/ApiData?dataUrl={Uri.EscapeDataString(dataUrl)}";

            return EleManager.ShowDrawer(
                title: "接口测试",
                url: url,
                //size: "70%",
                serverCloseHandler: "TestApiClosed"
                //closeAction: DrawerCloseAction.None
            );
        }

        public IActionResult OnPostTestApiClosed()
        {
            var idText = Request.Query["id"].ToString();
            if (!long.TryParse(idText, out var id) || id <= 0)
                return BuildResult(0, "success");

            var item = GisApi.GetDetail(id);
            if (item == null)
                return BuildResult(0, "success");

            var dataDtText = item.DataDt?.ToString("yyyy-MM-dd HH:mm:ss");
            return EleManager
                .SetControl<GisApi>(t => t.DataCnt, Value: item.DataCnt)
                .SetControl<GisApi>(t => t.DataDt, Value: dataDtText)
                .SetControl<GisApi>(t => t.LastErr, Value: item.LastErr)
                .ToActionResult();
        }
    }
}
