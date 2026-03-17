using App.Components;
using App.DAL;
using App.Utils;
using App.UIs;
using App.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tensorflow;
using App.Entities;

namespace App.Pages
{
    [Authorize]
    public class IndexModel : BaseModel
    {
        public List<Menu> Menus { get; set; } = new List<Menu>();
        public string UserName { get; set; }
        public string ProductVersion { get; set; }
        public string SiteTitle { get; set; }

        public async Task OnGetAsync()
        {
            UserName = GetUserName();
            ProductVersion = Common.GetVersion();
            SiteTitle = SiteConfig.Instance.Title;
            Menus = GetUserMenus();
        }

        // 获取用户可用的菜单列表
        private List<Menu> GetUserMenus()
        {
            var powers = GetPowers();
            return Menu.All
                .Where(m => m.Visible != false)
                .Where(m => m.Power == null || powers.Contains(m.Power.Value))
                .Each(m => {
                    m.ImageUrl = Asp.ResolveUrl(m.ImageUrl);
                    m.NavigateUrl = Asp.ResolveUrl(m.NavigateUrl);
                })
                .ToTree()
                ;
        }
    }
}
