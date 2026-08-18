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
        public string DisplayName { get; set; }
        public string ProductVersion { get; set; }
        public string SiteTitle { get; set; }

        public void OnGet()
        {
            var user = GetUser();
            UserName = user?.Name ?? GetUserName();
            DisplayName = user?.RealName.IsNotEmpty() == true
                ? user.RealName
                : (user?.NickName.IsNotEmpty() == true ? user.NickName : UserName);
            ProductVersion = Common.GetVersion();
            SiteTitle = SiteConfig.Instance.Title;
            Menus = GetUserMenus();
        }

        // 获取用户可用的菜单列表
        private List<Menu> GetUserMenus()
        {
            var userId = GetUserId();
            if (!userId.HasValue)
                return new List<Menu>();

            var roleMenuIds = RoleMenu.GetUserMenuIds(userId.Value);
            var menuMap = Menu.All.ToDictionary(t => t.Id, t => t);
            var allVisibleIds = new HashSet<long>();
            foreach (var menuId in roleMenuIds)
            {
                if (!menuMap.TryGetValue(menuId, out var menu))
                    continue;

                var current = menu;
                while (current != null)
                {
                    allVisibleIds.Add(current.Id);
                    if (!current.ParentId.HasValue || !menuMap.TryGetValue(current.ParentId.Value, out current))
                        break;
                }
            }

            return Menu.All
                .Where(m => m.Visible != false)
                .Where(m => allVisibleIds.Contains(m.Id))
                .Each(m => {
                    m.ImageUrl = Asp.ResolveUrl(m.ImageUrl);
                    m.NavigateUrl = Asp.ResolveUrl(m.NavigateUrl);
                })
                .ToTree()
                ;
        }
    }
}
