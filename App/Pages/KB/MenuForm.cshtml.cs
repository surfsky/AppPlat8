using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.HttpApi;
using App.Utils;
using App.Entities; // Added
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
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

            //if (Item == null) Item = new ArticleCategory();
            //if (parentId.HasValue && parentId != 0) Item.ParentId = parentId.Value;
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

            var item = req.Id > 0 ? KbMenu.Get(req.Id) : new KbMenu();
            item.Name = req.Name;
            item.SortId = req.SortId;
            item.ParentId = req.ParentId;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
