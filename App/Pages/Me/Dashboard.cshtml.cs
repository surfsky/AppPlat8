using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Utils;
using App.UIs;
using App.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
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

        public JsonResult OnGetPanelData(string theme = "light")
        {
            var list = GisPanel.Set
                .Where(t => t.InDashboard)
                .OrderBy(t => t.Position)
                .ThenBy(t => t.Id)
                .Select(t => new
                {
                    id = t.Id,
                    title = t.Title,
                    info = t.Info,
                    position = t.Position,
                    content = t.Content,
                    chartJson = t.ChartJson,
                    inGis = t.InGis,
                    inDashboard = t.InDashboard,
                    theme = string.IsNullOrWhiteSpace(theme) ? "light" : theme.Trim().ToLower(),
                })
                .ToList();
            return BuildResult(0, "success", list);
        }
    }
}
