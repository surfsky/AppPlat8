using App.Components;
using App.API;
using App.DAL;
using App.DAL.GIS;
using App.EleUI;
using App.Entities;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            var name = req.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                return BuildResult(400, "名称不能为空");

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

            // Avoid circular parent chains that can break full-name recursion.
            if (req.ParentId.HasValue)
            {
                var all = GisMenu.Set.Select(t => new { t.Id, t.ParentId }).ToList();
                var parentMap = all.ToDictionary(t => t.Id, t => t.ParentId);
                var visited = new HashSet<long>();
                var cursor = req.ParentId;
                while (cursor.HasValue)
                {
                    if (!visited.Add(cursor.Value))
                        return BuildResult(400, "菜单层级存在循环，请重新选择上级菜单");
                    if (req.Id > 0 && cursor.Value == req.Id)
                        return BuildResult(400, "上级菜单不能是当前菜单或其子菜单");

                    if (!parentMap.TryGetValue(cursor.Value, out var nextParent))
                        break;
                    cursor = nextParent;
                }
            }

            item.Name = name;
            item.ParentId = req.ParentId;
            item.Icon = req.Icon;
            item.IsDefaultShow = req.IsDefaultShow;
            item.Zoom = req.Zoom;
            item.Selectable = req.Selectable;
            item.SortId = req.SortId;
            item.DataFrom = req.DataFrom;
            item.DataUrl = req.DataUrl?.Trim();
            item.ItemUrl = req.ItemUrl?.Trim();
            item.DataCnt = req.DataCnt;
            item.DataDt = req.DataDt;

            try
            {
                item.Save();
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message ?? "未知错误";
                return BuildResult(400, $"保存失败：{msg}");
            }

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
                    // Use ISO datetime to keep client display and server JSON parsing consistent.
                    .SetControl<GisMenu>(t => t.DataDt, Value: now.ToString("yyyy-MM-ddTHH:mm:ss"))
                    .ToActionResult(msg);
            }
            catch (Exception ex)
            {
                return BuildResult(400, ex.Message);
            }
        }

        public IActionResult OnPostShowFiles([FromBody] GisMenu req)
        {
            var menuId = req?.Id ?? 0;
            if (menuId <= 0)
                return EleManager.ShowNotify("请先保存菜单，再维护附件", NotifyType.Warning, "提示");

            var uniId = req.UniId;
            var menuName = Uri.EscapeDataString(req?.Name ?? string.Empty);
            var url = $"/Shared/Atts?uniId={uniId}&name={menuName}&md={this.Mode}";
            return EleManager.ShowDrawer(
                title: "菜单附件",
                url: url,
                size: "50%",
                closeAction: DrawerCloseAction.RefreshData
            );
        }

        public IActionResult OnGetAttData(Paging pi, long id)
        {
            if (id <= 0)
                return BuildResult(0, "success", new { items = new List<object>(), total = 0 });

            var menu = GisMenu.GetDetail(id);
            if (menu == null)
                return BuildResult(0, "success", new { items = new List<object>(), total = 0 });

            var uniId = menu.UniId;
            if (string.IsNullOrWhiteSpace(uniId))
                return BuildResult(0, "success", new { items = new List<object>(), total = 0 });

            var pageIndex = pi?.PageIndex ?? 0;
            var pageSize = (pi?.PageSize ?? 10) > 0 ? pi.PageSize : 10;

            var query = Att.Set
                .Where(t => t.Key == uniId)
                .OrderBy(t => t.SortId)
                .ThenByDescending(t => t.Id);

            var total = query.Count();
            var rows = query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(t => (object)new
                {
                    id = t.Id,
                    name = string.IsNullOrWhiteSpace(t.FileName) ? Path.GetFileName(t.Url ?? string.Empty) : t.FileName,
                    sizeText = t.FileSizeText,
                    createDtText = t.CreateDt?.ToString("yyyy-MM-dd HH:mm"),
                    previewUrl = $"/Shared/FileViews/Viewer?uniId={Uri.EscapeDataString(uniId)}&id={t.Id}"
                })
                .ToList();

            return BuildResult(0, "success", new
            {
                items = rows,
                total
            });
        }
    }
}
