using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.EleUI;
using App.Entities;
using App.Pages;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [Auth(Permission.CheckSheetNew)]
    public class CheckTagsModel : AuthModel
    {        
        public CheckTag Item { get; set; }

        public void OnGet() { }

        /// <summary>获取标签树（按名称及类型筛选）</summary>
        public IActionResult OnGetData(string name = "", CheckTagType? tagType = null)
        {
            var q = new CheckTag().Query(name: name);
            if (tagType.HasValue)
            {
                var matchingIds = CheckTag.Set.Where(tag => tag.TagType == tagType.Value)
                    .Select(tag => tag.Id).ToHashSet();
                var ancestors = CheckTag.Set.ToList();
                foreach (var tag in ancestors.Where(tag => matchingIds.Contains(tag.Id)).ToList())
                {
                    var parentId = tag.ParentId;
                    while (parentId.HasValue)
                    {
                        if (!matchingIds.Add(parentId.Value)) break;
                        parentId = CheckTag.Get(parentId.Value)?.ParentId;
                    }
                }
                q = q.Where(tag => matchingIds.Contains(tag.Id));
            }
            var list = q.ToList();
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
                if (item == null) continue;
                // 检查是否有子菜单
                if (CheckTag.Set.Any(m => m.ParentId == id))
                    return BuildResult(400, $"菜单[{item.Name}]下还有子菜单，请先删除子菜单");

                item.Delete();
            }

            CheckTag.ClearCache(); // 刷新缓存
            return BuildResult(0, "删除成功");
        }


        public IActionResult OnPostMoveUp([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(CheckTag.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveUp(), "已经是最顶层");
            return EleHandler.Toast("上移成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveDown([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(CheckTag.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveDown(), "已经是最底层");
            return EleHandler.Toast("下移成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveFirst([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(CheckTag.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveFirst(), "已经是最顶层");
            return EleHandler.Toast("到顶成功", NotifyType.Success).ReloadData().ToResult();
        }

        public IActionResult OnPostMoveLast([FromBody] ElePayload payload)
        {
            Assert(payload?.SelectedCount == 1, "请先选择一行");
            var item = Exist(CheckTag.Get(payload.FirstSelectedId.Value));
            Assert(item.MoveLast(), "已经是最底层");
            return EleHandler.Toast("到底成功", NotifyType.Success).ReloadData().ToResult();
        }
    }
}
