using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.EleUI;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Permission.GisGeometryView)]
    public class MenusModel : AuthModel
    {
        public GisMenu Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData()
        {
            var items = GisMenu.GetTree();
            return BuildResult(0, "success", items);
        }

        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "请选择要删除的记录");
            if (!HasPermission(Permission.GisGeometryDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                var item = GisMenu.Get(id);
                if (item == null)
                    continue;

                if (GisMenu.Set.Any(m => m.ParentId == id))
                    return BuildResult(400, $"菜单[{item.Name}]下还有子菜单，请先删除子菜单");

                if (GisGeometry.Set.Any(g => g.MenuId == id))
                    return BuildResult(400, $"菜单[{item.Name}]下还有图形，请先迁移或删除图形");

                item.Delete();
            }

            GisMenu.ClearCache();
            return BuildResult(0, "删除成功");
        }


        //-------------------------------------------------------
        // Move
        //-------------------------------------------------------
        public IActionResult OnPostMoveUp([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(GisMenu.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveUp(), "已经是最顶层");
            return EleHandler.Toast("上移成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveDown([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(GisMenu.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveDown(), "已经是最底层");
            return EleHandler.Toast("下移成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveFirst([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(GisMenu.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveFirst(), "已经是最顶层");
            return EleHandler.Toast("到顶成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveLast([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(GisMenu.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveLast(), "已经是最底层");
            return EleHandler.Toast("到底成功", NotifyType.Success).ReloadData().ToResult();
        }

    }
}
