using System;
using System.Collections.Generic;
using System.Linq;
using App.DAL;
using App.Entities;
using App.Utils;
using Microsoft.EntityFrameworkCore;
using Z.EntityFramework.Plus;

namespace App.DAL.GIS
{
    /// <summary>
    /// 角色GIS菜单授权
    /// </summary>
    [UI("GIS", "角色GIS菜单")]
    public class RoleGisMenu : EntityBase<RoleGisMenu>
    {
        public long RoleId { get; set; }
        public long MenuId { get; set; }
        public bool CanSee { get; set; }
        public bool CanEdit { get; set; }

        public virtual Role Role { get; set; }
        public virtual GisMenu Menu { get; set; }

        static string RoleCacheKey(long roleId) => $"RoleGisMenu-Role-{roleId}";
        static string UserSeeCacheKey(long userId) => $"RoleGisMenu-UserSee-{userId}";
        static string UserEditCacheKey(long userId) => $"RoleGisMenu-UserEdit-{userId}";

        static void EnsureTable()
        {
            Db.Database.ExecuteSqlRaw(@"
CREATE TABLE IF NOT EXISTS RoleGisMenus (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    RoleId INTEGER NOT NULL,
    MenuId INTEGER NOT NULL,
    CanSee INTEGER NOT NULL,
    CanEdit INTEGER NOT NULL,
    CreateDt TEXT NULL,
    UpdateDt TEXT NULL,
    CreatorId INTEGER NULL,
    OwnerId INTEGER NULL
);");
            Db.Database.ExecuteSqlRaw("CREATE UNIQUE INDEX IF NOT EXISTS IX_RoleGisMenus_RoleId_MenuId ON RoleGisMenus(RoleId, MenuId);");
            Db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_RoleGisMenus_RoleId ON RoleGisMenus(RoleId);");
        }

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

        public static List<RoleGisMenu> GetRoleMenus(long roleId)
        {
            EnsureTable();
            return Cacher.Get(RoleCacheKey(roleId), () =>
                Set
                    .Where(t => t.RoleId == roleId)
                    .ToList()
            ) ?? new List<RoleGisMenu>();
        }

        public static List<long> GetUserVisibleMenuIds(long userId)
        {
            EnsureTable();
            return Cacher.Get(UserSeeCacheKey(userId), () => GetUserMenuIds(userId, true)) ?? new List<long>();
        }

        public static List<long> GetUserEditableMenuIds(long userId)
        {
            EnsureTable();
            return Cacher.Get(UserEditCacheKey(userId), () => GetUserMenuIds(userId, false)) ?? new List<long>();
        }

        static List<long> GetUserMenuIds(long userId, bool canSee)
        {
            var user = User.Set.FirstOrDefault(t => t.Id == userId);
            if (user == null)
                return new List<long>();

            if (string.Equals(user.Name, "admin", StringComparison.OrdinalIgnoreCase))
                return GisMenu.All.Select(t => t.Id).ToList();

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

        public static void SetRoleMenus(long roleId, List<RoleGisMenu> menus)
        {
            EnsureTable();
            var items = (menus ?? new List<RoleGisMenu>())
                .GroupBy(t => t.MenuId)
                .Select(g => g.Last())
                .Where(t => t.CanSee || t.CanEdit)
                .ToList();

            Set.Where(t => t.RoleId == roleId).Delete();
            foreach (var item in items)
            {
                new RoleGisMenu
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
