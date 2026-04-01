using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.AI
{
    [CheckPower(Power.ConfigAI)]
    public class AIConfigFormModel : AdminModel
    {
        public AIConfig Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id)
        {
            var item = id > 0 ? AIConfig.Get(id) : new AIConfig();
            if (item == null)
                return BuildResult(404, "对象不存在");
            return BuildResult(0, "success", item.Export());
        }

        public IActionResult OnPostSave([FromBody] AIConfig req)
        {
            if (!CheckPower(Power.ConfigAI))
                return BuildResult(403, "无权操作");
            if (req == null)
                return BuildResult(400, "参数错误");
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.BaseUrl) || string.IsNullOrWhiteSpace(req.Model))
                return BuildResult(400, "名称、地址、模型不能为空");

            AIConfig item;
            if (req.Id == 0)
            {
                item = new AIConfig();
            }
            else
            {
                item = AIConfig.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "对象不存在");
            }

            item.Name = req.Name?.Trim();
            item.BaseUrl = req.BaseUrl?.Trim().TrimEnd('/');
            item.ApiKey = req.ApiKey?.Trim();
            item.Model = req.Model?.Trim();
            item.Services = string.IsNullOrWhiteSpace(req.Services) ? "chat" : req.Services.Trim();
            item.TimeoutSeconds = req.TimeoutSeconds <= 0 ? 60 : req.TimeoutSeconds;
            item.SortId = req.SortId;
            item.IsDefault = req.IsDefault;
            item.InUsed = req.InUsed;
            item.Remark = req.Remark;
            item.Save();

            if (item.IsDefault)
            {
                var others = AIConfig.Set.Where(t => t.Id != item.Id && t.IsDefault).ToList();
                foreach (var other in others)
                {
                    other.IsDefault = false;
                    other.Save();
                }
            }

            return BuildResult(0, "保存成功", item.Export());
        }
    }
}
