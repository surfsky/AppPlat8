using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Pages;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    public class CheckTagsModel : BaseModel
    {        
        public CheckTag Item { get; set; }

        public void OnGet() { }

        /// <summary>获取标签树（支持按名称模糊、按组织过滤）</summary>
        public IActionResult OnGetData(string name = "", long? orgId = null)
        {
            // 先按条件过滤（名称模糊包含 + 组织精确匹配），再 ToTree 形成父子结构
            var list = new CheckTag().Query(name: name, orgId: orgId).ToList();
            var items = list.ToTree();
            return BuildResult(0, "success", items);
        }

        /// <summary>删除菜单</summary>
        public IActionResult OnPostDelete([FromBody] long[] ids)
        {
            if (ids == null || ids.Length == 0) 
                return BuildResult(400, "请选择要删除的记录");
            foreach (var id in ids)
            {
                var item = CheckTag.Get(id);
                if (item != null)
                {
                    // 检查是否有子菜单
                    if (CheckTag.Set.Any(m => m.ParentId == id))
                        return BuildResult(400, $"菜单[{item.Name}]下还有子菜单，请先删除子菜单");

                    item.Delete();
                }
            }

            CheckTag.ClearCache(); // 刷新缓存
            return BuildResult(0, "删除成功");
        }
    }
}
