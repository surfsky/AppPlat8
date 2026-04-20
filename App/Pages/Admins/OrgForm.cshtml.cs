using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using App.Components;
using App.DAL;
using App.HttpApi;
using App.Utils;
using App.Entities;
using App.EleUI;

namespace App.Pages.Admins
{


    [CheckPower(Power.OrgView)]
    public class OrgFormModel : AdminModel
    {
        public App.DAL.Org Item { get; set; }

        /// <summary>获取组织详情</summary>
        public void OnGet() { }

        /// <summary>获取数据(Vue API)</summary>
        public IActionResult OnGetData(int id, long? selectId)
        {
            var item = id > 0 ? App.DAL.Org.Get(id) : new App.DAL.Org();
            if (id == 0)
                item.ParentId = selectId;

            return BuildResult(0, "success", item.Export());
        }

        /// <summary>保存组织</summary>
        public async Task<IActionResult> OnPostSave([FromBody] App.DAL.Org req)
        {
            if (req == null) 
                return BuildResult(400, "参数错误");
            if (req.Name.IsEmpty()) 
                return BuildResult(400, "名称不能为空");

            App.DAL.Org item;
            if (req.Id == 0)
            {
                if (!CheckPower(Power.OrgNew)) 
                    return BuildResult(403, "无权新增");
                item = new App.DAL.Org();
            }
            else
            {
                if (!CheckPower(Power.OrgEdit)) 
                    return BuildResult(403, "无权编辑");
                item = App.DAL.Org.Get(req.Id);
                if (req.ParentId == req.Id)
                    return BuildResult(400, "上级组织不能是自己");
            }

            // 
            item.Name = req.Name;
            item.ParentId = req.ParentId;
            item.SortId = req.SortId;
            item.Remark = req.Remark;
            item.Save();
            App.DAL.Org.ClearCache(); // Refresh cache
            return BuildResult(0, "保存成功");
        }
    }
}
