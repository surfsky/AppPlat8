using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryEdit)]
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
            item.OrgId = req.OrgId;
            item.SortId = req.SortId;
            item.DataCount = req.DataCount;
            item.DataDt = req.DataDt;
            item.Save();

            GisMenu.ClearCache();
            return BuildResult(0, "保存成功");
        }
    }
}
