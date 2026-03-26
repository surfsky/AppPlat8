using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using App.DAL;
using App.Web;
using App.Components;
using App.Utils;

namespace App.Pages.Maintains
{
    [CheckPower(Power.ConfigSite)]
    public class SiteConfigFormModel : AdminModel
    {
        [BindProperty]
        public SiteConfig Item { get; set; }

        public void OnGet(){}
        public IActionResult OnGetData()
        {
            Item = SiteConfig.Instance ?? new SiteConfig();
            return BuildResult(0, "success", Item);
        }

        public IActionResult OnPostSave([FromBody] SiteConfig req)
        {
            if (!CheckPower(Power.ConfigSite))
                return BuildResult(403, "无权操作");

            var cfg = SiteConfig.Set.FirstOrDefault();
            if (cfg == null)
                cfg = new SiteConfig();

            // 更新属性
            cfg.Title = req.Title;
            cfg.BeiAnNo = req.BeiAnNo;
            cfg.Icon = Uploader.SaveFile(nameof(SiteConfig), req.Icon);
            cfg.LoginBg = Uploader.SaveFile(nameof(SiteConfig), req.LoginBg);
            cfg.PageSize = req.PageSize;
            cfg.DefaultPassword = req.DefaultPassword;
            cfg.UpFileTypes = req.UpFileTypes;
            cfg.UpFileSize = req.UpFileSize;
            cfg.MapKey = req.MapKey;

            cfg.Save();
            SiteConfig.ClearCache();
            return BuildResult(0, "保存成功", cfg);
        }
    }
}
