using System;
using System.Collections.Generic;
using System.Linq;
using App.Components; // Added
using App.EleUI; // Added for EleTreeNode and EleHelper
using App.DAL;
using App.Entities;
using App.Pages;
using App.Utils; // Added for EleHelper
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Maintains
{
    public class MenuFormModel : AdminModel
    {
        public Menu Item { get; set; }
        public List<TreeItem> MenuTree { get; set; } // Changed type
        public List<ListItem> PowerOptions { get; set; }

        public void OnGet(long? id)
        {
            MenuTree     = EleHelper.ToTreeItems(Menu.All, null);
            PowerOptions = EleHelper.ToListItems(typeof(Power));
        }

        public IActionResult OnGetData(long id, long? selectId)
        {
            var item = Menu.Get(id) ?? new Menu();
            if (id == 0)
                item.ParentId = selectId;
            return BuildResult(0, "success", item.Export());
        }

        public IActionResult OnPostSave([FromBody] Menu req)
        {
            if (req == null) return BuildResult(400, "参数错误");

            Menu item;
            if (req.Id == 0)
            {
                item = new Menu();
            }
            else
            {
                item = Menu.Get(req.Id);
                if (item == null) return BuildResult(404, "对象不存在");
                if (req.ParentId == req.Id) return BuildResult(400, "上级菜单不能是自己");
            }

            item.Name = req.Name;
            item.ParentId = req.ParentId;
            item.NavigateUrl = req.NavigateUrl;
            item.Power = req.Power;
            item.ImageUrl = req.ImageUrl;
            item.SortId = req.SortId;
            item.Target = req.Target;
            item.Visible = req.Visible;
            item.Expanded = req.Expanded;
            item.Remark = req.Remark;
            item.Fixed = req.Fixed;

            item.Save();
            Menu.ClearCache();
            return BuildResult(0, "保存成功");
        }


    }
}
