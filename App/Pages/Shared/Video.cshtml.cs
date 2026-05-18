using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.Shared
{
    /// <summary>
    /// 通用视频监控页面
    /// 支持通过查询参数 urls (JSON数组) 或 url (单个) 传递视频地址
    /// 也支持通过 localStorage('gis_video_urls') 传递多个视频 url 参数
    /// </summary>
    public class VideoModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
