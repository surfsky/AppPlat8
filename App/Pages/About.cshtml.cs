using System.Reflection;
using App.Components;
using App.DAL;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages
{
    /// <summary>
    /// 关于本系统页面（公开访问，作为产品官网入口）
    /// </summary>
    [AllowAnonymous]
    public class AboutModel : BaseModel
    {
        [BindProperty] public string SiteTitle { get; set; }
        [BindProperty] public string ProductVersion { get; set; }
        [BindProperty] public string Author { get; set; } = "surfsky";
        [BindProperty] public string ProjectUrl { get; set; } = "https://github.com/surfsky/AppPlat8";
        [BindProperty] public string License { get; set; } = "MIT";
        [BindProperty] public string NetVersion { get; set; }
        [BindProperty] public string BuildTime { get; set; }

        public void OnGet()
        {
            SiteTitle = SiteConfig.Instance.Title;
            ProductVersion = Common.GetVersion();

            try
            {
                NetVersion = System.Environment.Version.ToString();
            }
            catch { NetVersion = "-"; }

            try
            {
                var asm = Assembly.GetEntryAssembly();
                if (asm != null)
                {
                    var fi = new System.IO.FileInfo(asm.Location);
                    if (fi.Exists)
                        BuildTime = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }
            catch { BuildTime = "-"; }
        }
    }
}
