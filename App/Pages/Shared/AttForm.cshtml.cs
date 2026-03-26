using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Shared
{
    [CheckPower(Power.CheckObjectEdit)]
    public class AttFormModel : AdminModel
    {
        [BindProperty(SupportsGet = true)]
        public string UniId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Name { get; set; }

        public Att Item { get; set; }

        public void OnGet(string uniId, string name)
        {
            UniId = uniId?.Trim();
            Name = name;
        }

        private object BuildFormData(Att item, string uniId)
        {
            return new
            {
                id = item.Id,
                key = item.Key,
                type = item.Type,
                content = item.Content,
                fileName = item.FileName,
                remark = item.Remark,
                sortId = item.SortId,
                protect = item.Protect,
                uniId = uniId
            };
        }

        public IActionResult OnGetData(long id, string uniId)
        {
            uniId = uniId?.Trim();
            if (string.IsNullOrWhiteSpace(uniId))
                return BuildResult(400, "参数错误：缺少uniId");

            if (id <= 0)
                return BuildResult(0, "success", BuildFormData(new Att { Key = uniId, Type = AttType.File }, uniId));

            var item = Att.Get(id);
            if (item == null)
                return BuildResult(404, "附件不存在");
            if (!string.Equals(item.Key, uniId, StringComparison.OrdinalIgnoreCase))
                return BuildResult(403, "无权访问该附件");

            return BuildResult(0, "success", BuildFormData(item, uniId));
        }

        public IActionResult OnPostSave([FromBody] Att req, string uniId)
        {
            uniId = uniId?.Trim();
            if (string.IsNullOrWhiteSpace(uniId))
                return BuildResult(400, "参数错误：缺少uniId");
            if (req == null)
                return BuildResult(400, "参数错误");
            if (string.IsNullOrWhiteSpace(req.Content))
                return BuildResult(400, "附件地址不能为空");

            Att item;
            if (req.Id > 0)
            {
                item = Att.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "附件不存在");
                if (!string.Equals(item.Key, uniId, StringComparison.OrdinalIgnoreCase))
                    return BuildResult(403, "无权操作该附件");
            }
            else
            {
                item = new Att
                {
                    Key = uniId
                };
            }

            item.Key = uniId;
            item.Type = req.Type ?? AttType.File;
            item.Content = Uploader.SaveFile(nameof(Att), req.Content);
            item.FileName = string.IsNullOrWhiteSpace(req.FileName)
                ? item.Content?.Split('/').LastOrDefault()
                : req.FileName.Trim();
            item.Remark = req.Remark;
            item.SortId = req.SortId;
            item.Protect = req.Protect;
            item.Save();

            return BuildResult(0, "保存成功", new { id = item.Id });
        }
    }
}
