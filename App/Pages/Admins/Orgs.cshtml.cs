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

namespace App.Pages.Admins
{
    [CheckPower(Power.OrgView)]
    public class OrgsModel : AdminModel
    {
        public App.DAL.Org Item { get; set; }

        public void OnGet(){}

        /// <summary>获取清单</summary>
        public async Task<IActionResult> OnGetData(string name)
        {
            var allOrgs = await App.DAL.Org.Set.OrderBy(o => o.SortId).ToListAsync();
            if (!string.IsNullOrEmpty(name))
                allOrgs = allOrgs.Where(o => o.Name.Contains(name)).ToList();
            
            // Convert to nested structure
            var tree = allOrgs.ToTree();
            return BuildResult(0, "success", tree, null);
        }


        public async Task<IActionResult> OnPostDelete([FromBody]long[] ids)
        {
            if (ids == null || ids.Length == 0)
                return BuildResult(400, "参数错误");
            if (!CheckPower(Power.OrgDelete))
                return BuildResult(403, "无权操作");

            foreach (var id in ids)
            {
                // Check users
                int userCount = await App.DAL.User.Set.Where(u => u.OrgId == id).CountAsync();
                if (userCount > 0) return BuildResult(400, $"删除失败！组织[{id}]下还有用户");

                // Check children
                int childCount = await App.DAL.Org.Set.Where(d => d.ParentId == id).CountAsync();
                if (childCount > 0) return BuildResult(400, $"删除失败！组织[{id}]下还有子组织");

                var item = App.DAL.Org.Get(id);
                if (item != null)
                {
                    item.Delete();
                }
            }
            App.DAL.Org.ClearCache();
            return BuildResult(0, "删除成功");
        }
    }
}
