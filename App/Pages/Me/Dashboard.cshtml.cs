using App.Components;
using App.DAL;
using App.Utils;
using App.UIs;
using App.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace App.Pages
{
    [Authorize]
    public class DashboardModel : BaseModel
    {
        public string SiteTitle { get; set; }

        public async Task OnGetAsync()
        {
            SiteTitle = SiteConfig.Instance.Title;
        }
    }
}
