using System;
using System.Collections.Generic;
using System.Linq;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;
using Z.EntityFramework.Plus;

namespace App.DAL
{
    /// <summary>
    /// 角色菜单授权（系统菜单）
    /// </summary>
    [UI("系统", "角色菜单")]
    public class RoleMenu : EntityBase<RoleMenu>
    {
        public long RoleId { get; set; }
        public long MenuId { get; set; }

        public virtual Role Role { get; set; }
        public virtual Menu Menu { get; set; }

        static string RoleCacheKey(long roleId) => $"RoleMenu-Role-{roleId}";
        static string UserCacheKey(long userId) => $"RoleMenu-User-{userId}";

        static void EnsureTable()
        {
            Db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS RoleMenus (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    RoleId INTEGER NOT NULL,
    MenuId INTEGER NOT NULL,
    CreateDt TEXT NULL,
    UpdateDt TEXT NULL,
    CreatorId INTEGER NULL,
    OwnerId INTEGER NULL
);");
            Db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_RoleMenus_RoleId_MenuId ON RoleMenus(RoleId, MenuId);");
            Db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_RoleMenus_RoleId ON RoleMenus(RoleId);");
        }

        static void ClearRoleCache(long roleId)
        {
            Cacher.Remove(RoleCacheKey(roleId));
            var userIds = User.Set
                .Where(t => t.Roles.Any(r => r.Id == roleId))
                .Select(t => t.Id)
                .ToList();
            foreach (var userId in userIds)
                Cacher.Remove(UserCacheKey(userId));
        }

        public static List<long> GetRoleMenuIds(long roleId)
        {
            EnsureTable();
            return Cacher.Get(RoleCacheKey(roleId), () =>
                Set
                    .Where(t => t.RoleId == roleId)
                    .Select(t => t.MenuId)
                    .Distinct()
                    .ToList()
            ) ?? new List<long>();
        }

        public static List<long> GetUserMenuIds(long userId)
        {
            EnsureTable();
            return Cacher.Get(UserCacheKey(userId), () =>
            {
                var user = User.Set.FirstOrDefault(t => t.Id == userId);
                if (user == null)
                    return new List<long>();

                if (string.Equals(user.Name, "admin", StringComparison.OrdinalIgnoreCase))
                    return Menu.All.Where(t => t.Visible != false).Select(t => t.Id).ToList();

                var roleIds = User.Set
                    .Where(t => t.Id == userId)
                    .SelectMany(t => t.Roles)
                    .Select(t => t.Id)
                    .ToList();

                if (roleIds.Count == 0)
                    return new List<long>();

                return Set
                    .Where(t => roleIds.Contains(t.RoleId))
                    .Select(t => t.MenuId)
                    .Distinct()
                    .ToList();
            }) ?? new List<long>();
        }

        public static void SetRoleMenus(long roleId, List<long> menuIds)
        {
            EnsureTable();
            var ids = (menuIds ?? new List<long>()).Distinct().ToList();
            Set.Where(t => t.RoleId == roleId).Delete();
            foreach (var menuId in ids)
            {
                new RoleMenu
                {
                    RoleId = roleId,
                    MenuId = menuId,
                }.Save();
            }

            ClearRoleCache(roleId);
        }
    }
}
