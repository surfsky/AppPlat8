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

        static string RoleCacheKey(long roleId) => $"RoleContactMenu-Role-{roleId}";
        static string UserSeeCacheKey(long userId) => $"RoleContactMenu-UserSee-{userId}";
        static string UserEditCacheKey(long userId) => $"RoleContactMenu-UserEdit-{userId}";



        static void ClearRoleCache(long roleId)
        {
            Cacher.Remove(RoleCacheKey(roleId));
            var userIds = User.Set
                .Where(t => t.Roles.Any(r => r.Id == roleId))
                .Select(t => t.Id)
                .ToList();
            foreach (var userId in userIds)
            {
                Cacher.Remove(UserSeeCacheKey(userId));
                Cacher.Remove(UserEditCacheKey(userId));
            }
        }

        public static List<RoleContactMenu> GetRoleMenus(long roleId)
        {
            return Cacher.Get(RoleCacheKey(roleId), () =>
                Set
                    .Where(t => t.RoleId == roleId)
                    .ToList()
            ) ?? new List<RoleContactMenu>();
        }

        public static List<long> GetUserVisibleMenuIds(long userId)
        {
            return Cacher.Get(UserSeeCacheKey(userId), () => GetUserMenuIds(userId, true)) ?? new List<long>();
        }

        public static List<long> GetUserEditableMenuIds(long userId)
        {
            return Cacher.Get(UserEditCacheKey(userId), () => GetUserMenuIds(userId, false)) ?? new List<long>();
        }

        static List<long> GetUserMenuIds(long userId, bool canSee)
        {
            var user = User.Set.FirstOrDefault(t => t.Id == userId);
            if (user == null)
                return new List<long>();

            if (string.Equals(user.Name, "admin", StringComparison.OrdinalIgnoreCase))
                return ContactMenu.All.Select(t => t.Id).ToList();

            var roleIds = User.Set
                .Where(t => t.Id == userId)
                .SelectMany(t => t.Roles)
                .Select(t => t.Id)
                .ToList();

            if (roleIds.Count == 0)
                return new List<long>();

            var q = Set.Where(t => roleIds.Contains(t.RoleId));
            q = canSee ? q.Where(t => t.CanSee) : q.Where(t => t.CanEdit);

            return q.Select(t => t.MenuId).Distinct().ToList();
        }

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

            ClearRoleCache(roleId);
        }
    }
}
