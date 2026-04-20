using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace App.Pages.OA
{
    [Auth(Power.AssetView)]
    [IgnoreAntiforgeryToken]
    public class AssetFormModel : AdminModel
    {
        [BindProperty]
        public Asset Item { get; set; }
        public List<App.DAL.Org> OrgTree { get; set; }
        
        public void OnGet()
        {
            OrgTree = App.DAL.Org.GetTree();
        }

        /// <summary>获取用户列表</summary>
        public IActionResult OnGetUsers(string keyword)
        {
            var query = App.DAL.User.Set.Where(u => u.InUsed == true);
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(u => u.Name.Contains(keyword) || u.RealName.Contains(keyword));
            }
            
            var list = query.Take(20).Select(u => new 
            {
                id = u.Id,
                name = u.RealName ?? u.Name,
                username = u.Name,
                dept = u.Org != null ? u.Org.Name : ""
            }).ToList();
            
            return BuildResult(0, "success", list);
        }

        /// <summary>获取资产详情</summary>
        public IActionResult OnGetData(long id)
        {
            if (id == 0)
            {
                if (!CheckPower(Power.AssetNew) && !CheckPower(Power.AssetEdit) && !CheckPower(Power.AssetView))
                    return BuildResult(403, "无权查看");
                
                return BuildResult(0, "success", new Asset());
            }
            else
            {
                if (!CheckPower(Power.AssetView) && !CheckPower(Power.AssetEdit))
                    return BuildResult(403, "无权查看");
                
                var item = Asset.Get(id);
                if (item == null)
                    return BuildResult(404, "无效参数");
                
                return BuildResult(0, "success", item.Export());
            }
        }

        /// <summary>保存资产</summary>
        public IActionResult OnPostSave([FromBody] Asset req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
                return BuildResult(400, "名称不能为空");
            var needPower = (req.Id == 0) ? Power.AssetNew : Power.AssetEdit;
            if (!CheckPower(needPower))
                return BuildResult(403, "无权操作");

            Asset item = req.Id > 0 ? Asset.Get(req.Id) : new Asset();
            item.Name = req.Name;
            item.Category = req.Category;
            item.OrgId = req.OrgId;
            item.ChargeUserId = req.ChargeUserId;
            item.Location = req.Location;
            item.Manufacturer = req.Manufacturer;
            item.Image = Uploader.SaveFile(nameof(Asset), req.Image);
            item.Parameters = req.Parameters;
            item.EnableDt = req.EnableDt;
            item.ExpireDt = req.ExpireDt;
            item.IsExpireAlert = req.IsExpireAlert;
            item.Save();                
            return BuildResult(0, "保存成功", new { id = item.Id });
        }
    }
}
