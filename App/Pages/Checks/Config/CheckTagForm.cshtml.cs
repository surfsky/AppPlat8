using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using App.Components; // Added
using App.EleUI; // Added for EleTreeNode and EleHelper
using App.DAL;
using App.Entities;
using App.Pages;
using App.Utils; // Added for EleHelper
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Checks
{
    [Auth(Power.CheckSheetNew)]
    public class CheckTagFormModel : AuthModel
    {
        public CheckTag Item { get; set; }

        public IActionResult OnPostParentChanged([FromBody] ControlChangeRequest req)
        {
            long parentId = 0;
            if (req?.Value is JsonElement value && value.ValueKind == JsonValueKind.Number)
                value.TryGetInt64(out parentId);
            else if (req?.Value != null)
                long.TryParse(req.Value.ToString(), out parentId);

            var parent = parentId > 0 ? CheckTag.Get(parentId) : null;
            var tagType = parent?.TagType ?? CheckTagType.Basic;
            if (parent == null && req?.Form.ValueKind == JsonValueKind.Object
                && req.Form.TryGetProperty("tagType", out var currentType)
                && currentType.ValueKind == JsonValueKind.Number
                && Enum.IsDefined(typeof(CheckTagType), currentType.GetInt32()))
            {
                tagType = (CheckTagType)currentType.GetInt32();
            }
            return BuildResult(0, "success", new { tagType });
        }

        public void OnGet(long? id) {}

        public IActionResult OnGetData(long id, long? selectId)
        {
            var item = CheckTag.GetDetail(id) ?? new CheckTag();
            if (id == 0)
            {
                item.ParentId = selectId;
                if (selectId.HasValue)
                {
                    var parent = CheckTag.Get(selectId.Value);
                    if (parent == null)
                        return BuildResult(400, "上级标签不存在");
                    item.TagType = parent.TagType;
                }
            }
            return BuildResult(0, "success", item.Export());
        }

        public IActionResult OnPostSave([FromBody] CheckTag req)
        {
            if (req == null) 
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? CheckTag.Get(req.Id) : new CheckTag();
            if (req.Id > 0 && item == null)
                return BuildResult(404, "标签不存在");

            CheckTag parent = null;
            if (req.ParentId.HasValue)
            {
                parent = CheckTag.Get(req.ParentId.Value);
                if (parent == null)
                    return BuildResult(400, "上级标签不存在");

                var ancestor = parent;
                while (ancestor != null)
                {
                    if (ancestor.Id == item.Id && item.Id > 0)
                        return BuildResult(400, "不能将标签移动到自身或其子级下");
                    ancestor = ancestor.ParentId.HasValue ? CheckTag.Get(ancestor.ParentId.Value) : null;
                }
            }

            item.Name = req.Name;
            item.ParentId = req.ParentId;
            item.SortId = req.SortId;
            item.TagType = req.TagType;
            item.Save();
            CheckTag.ClearCache();

            foreach (var descendant in CheckTag.All.GetDescendants(item.Id).Where(tag => tag.Id != item.Id))
            {
                descendant.TagType = item.TagType;
                descendant.Save();
            }

            CheckTag.ClearCache();
            return BuildResult(0, "保存成功");
        }


    }
}
