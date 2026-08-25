using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using App.EleUI;

namespace App.Pages.KB
{
    [Auth(Power.KbMenuView, Power.KbMenuEdit)]
    public class MenuFormModel : AdminModel
    {
        public KbMenu Item { get; set; }
        public List<KbMenu> MenuTree { get; set; }

        public void OnGet(long? parentId)
        {
            this.MenuTree = KbMenu.GetTree();
            Item = new KbMenu { ParentId = parentId };
        }

        public IActionResult OnGetData(long id, long? selectId)
        {
            var item = KbMenu.GetDetail(id);
            if (item == null)
            {
                item = new KbMenu();
                item.ParentId = selectId;
            }
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] KbMenu req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (req.Name.IsEmpty())
                return BuildResult(400, "名称不能为空");

            var item = req.Id > 0 ? KbMenu.Get(req.Id) : new KbMenu();
            if (req.Id > 0 && item == null)
                return BuildResult(404, "目录不存在");
            if (req.ParentId == req.Id)
                return BuildResult(400, "上级目录不能是自己");

            item.Name = req.Name;
            item.SortId = req.SortId;
            item.ParentId = req.ParentId;
            item.OrgId = req.OrgId;
            item.Save();
            KbMenu.ClearCache();
            return BuildResult(0, "保存成功");
        }
    }
}
