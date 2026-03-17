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

namespace App.Pages.Checks
{
    public class CheckTagFormModel : AdminModel
    {
        public CheckTag Item { get; set; }

        public void OnGet(long? id)
        {
        }

        public IActionResult OnGetData(long id, long? selectId)
        {
            var item = CheckTag.GetDetail(id) ?? new CheckTag();
            if (id == 0)
                item.ParentId = selectId;
            return BuildResult(0, "success", item.Export());
        }

        public IActionResult OnPostSave([FromBody] CheckTag req)
        {
            if (req == null) return BuildResult(400, "参数错误");

            CheckTag item = req.Id >0 ? CheckTag.Get(req.Id) : new CheckTag();
            if (req.Id > 0)
            {
                item = CheckTag.Get(req.Id);
                if (req.ParentId == req.Id) return BuildResult(400, "上级不能是自己");
            }

            item.Name = req.Name;
            item.ParentId = req.ParentId;
            item.SortId = req.SortId;
            item.OrgId = req.OrgId;
            item.Save();
            CheckTag.ClearCache();
            return BuildResult(0, "保存成功");
        }


    }
}
