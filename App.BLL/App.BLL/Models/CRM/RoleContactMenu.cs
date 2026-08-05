using System;
using System.Collections.Generic;
using System.Linq;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;
using Z.EntityFramework.Plus;

namespace App.DAL.OA
{
    /// <summary>
    /// 角色通讯录目录授权（角色可授权某个通讯录目录的查看和编辑权限）
    /// </summary>
    [UI("OA", "角色通讯录目录")]
    public class RoleContactMenu : EntityBase<RoleContactMenu>
    {
        public long RoleId { get; set; }
        public long MenuId { get; set; }
        public bool CanSee { get; set; }
        public bool CanEdit { get; set; }

        public virtual Role Role { get; set; }
        public virtual ContactMenu Menu { get; set; }


        /// <summary>获取用户可查看或编辑的通讯录目录IDs</summary>
        public static List<long> GetUserMenuIds(long userId, bool canSee)
        {
            var user = User.GetDetail(userId);
            if (user == null)
                return new List<long>();
            if (string.Equals(user.Name, "admin", StringComparison.OrdinalIgnoreCase))
                return ContactMenu.All.Select(t => t.Id).ToList();

            // get roleIds
            var roleIds = user.GetRoleIds();
            if (roleIds.Count == 0)
                return new List<long>();

            // get menuids，改为从缓存中获取，避免每次都查询数据库
            var all = RoleContactMenu.All;
            return all
                .Where(t => roleIds.Contains(t.RoleId))
                .Where(t => canSee ? t.CanSee : t.CanEdit)
                .Select(t => t.MenuId)
                .Distinct()
                .ToList();
        }

        /// <summary>获取某个角色授权的通讯录目录</summary>
        public static List<RoleContactMenu> GetRoleMenus(long roleId)
        {
            return RoleContactMenu.All.Where(t => t.RoleId == roleId).ToList();
        }

        /// <summary>设置角色通讯录目录IDs</summary>
        public static void SetRoleMenus(long roleId, List<RoleContactMenu> menus)
        {
            var items = (menus ?? new List<RoleContactMenu>())
                .GroupBy(t => t.MenuId)
                .Select(g => g.Last())
                .Where(t => t.CanSee || t.CanEdit)
                .ToList();

            Set.Where(t => t.RoleId == roleId).Delete();
            foreach (var item in items)
            {
                new RoleContactMenu
                {
                    RoleId = roleId,
                    MenuId = item.MenuId,
                    CanSee = item.CanSee,
                    CanEdit = item.CanEdit,
                }.Save();
            }
            ClearCache();
        }
    }
}
