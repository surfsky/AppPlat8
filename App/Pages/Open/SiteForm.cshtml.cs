using System;
using App.Components;
using App.DAL;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.Open
{
    /// <summary>参考网站编辑</summary>
    [Auth(Power.SiteView)]
    [IgnoreAntiforgeryToken]
    public class SiteFormModel : AdminModel
    {
        [BindProperty]
        public Site Item { get; set; }

        /// <summary>页面</summary>
        public void OnGet() { }

        /// <summary>详情</summary>
        public IActionResult OnGetData(long id)
        {
            var item = Site.Get(id);
            if (item == null)
                return BuildResult(404, "无效参数");
            return BuildResult(0, "success", item.Export());
        }

        /// <summary>保存</summary>
        public IActionResult OnPostSave([FromBody] Site req)
        {
            try
            {
                if (req == null || req.Type.IsEmpty() || req.Name.IsEmpty() || req.Url.IsEmpty())
                    return BuildResult(400, "分组、名称、网址不能为空");

                var needPower = req.Id == 0 ? Power.SiteNew : Power.SiteEdit;
                if (!CheckPower(needPower))
                    return BuildResult(403, "无权操作");

                var item = req.Id == 0 ? new Site() : Site.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "记录不存在");

                item.Type = req.Type?.Trim();
                item.Name = req.Name?.Trim();
                item.Desc = req.Desc?.Trim();
                item.Url = req.Url?.Trim();
                item.Icon = Uploader.SaveFile(nameof(User), req.Icon);
                item.SortId = req.SortId;
                item.Save(null, log: true);
                return BuildResult(0, "保存成功", new { id = item.Id });
            }
            catch (Exception ex)
            {
                Logger.Error("Site Save Fail: {0}", ex.ToString());
                return BuildResult(500, ex.Message);
            }
        }
    }
}
