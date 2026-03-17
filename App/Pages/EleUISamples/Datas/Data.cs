using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using App.DAL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace App.Pages.EleUISamples
{
    /// <summary>角色</summary>
    public class Role
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Remark { get; set; }
    }

    /// <summary>部门</summary>
    public class Dept
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public List<Dept> Children { get; set; }
    }

    /// <summary>用户实体</summary>
    public class User
    {
        [Display(Name = "主键")]              public long Id { get; set; }
        [Display(Name = "姓名")] [Required] public string Name { get; set; }
        [Display(Name = "中文名")]            public string ChineseName { get; set; }
        [Display(Name = "角色名")]            public string RoleName { get; set; }
        [Display(Name = "年龄")]              public int Age { get; set; }
        [Display(Name = "金额")]              public decimal Score { get; set; }
        [Display(Name = "日期")]              public DateTime Birthday { get; set; }
        [Display(Name = "开关")]              public bool IsEnabled { get; set; }
        [Display(Name = "单选(Radio)")]       public string Gender { get; set; }
        [Display(Name = "下拉(Select)")]      public string City { get; set; }
        [Display(Name = "多选(MultiSelect)")] public List<string> Types { get; set; }
        [Display(Name = "多行文本")]           public string Description { get; set; }
        [Display(Name = "图片")]               public string Photo { get; set; }
        [Display(Name = "多图片")]             public List<string> Photos { get; set; }
        [Display(Name = "图标(IconSelect)")]   public string Icon { get; set; }
        [Display(Name = "用户(Selector)")]     public string UserId { get; set; }
        [Display(Name = "组织")]             public string OrgId { get; set; }
        [Display(Name = "部门")]             public long? DeptId { get; set; } 
        [Display(Name = "角色")]             public List<long> RoleIds { get; set; }
    }

    /// <summary>
    /// 测试数据
    /// </summary>
    public class Data
    {
        private static readonly object _syncRoot = new object();

        private static List<Role> _roles = new List<Role>
        {
            new Role { Id = 1, Name = "Admin", Remark = "系统管理员" },
            new Role { Id = 2, Name = "Manager", Remark = "经理" },
            new Role { Id = 3, Name = "User", Remark = "普通用户" }
        };

        private static List<User> _users = new List<User>
        {
            new User { Id = 1, Name = "admin", ChineseName = "管理员", RoleName = "Admin" },
            new User { Id = 2, Name = "zhangsan", ChineseName = "张三", RoleName = "Manager" },
            new User { Id = 3, Name = "lisi", ChineseName = "李四", RoleName = "User" },
            new User { Id = 4, Name = "wangwu", ChineseName = "王五", RoleName = "User" },
            new User { Id = 5, Name = "zhaoliu", ChineseName = "赵六", RoleName = "Manager" }
        };

        public static List<Role> GetRoles() => _roles;

        public static User GetUser(long id)
        {
            if (id <= 0)
                return null;
            lock (_syncRoot)
            {
                var user = _users.FirstOrDefault(t => t.Id == id);
                return user == null ? null : CloneUser(user);
            }
        }

        public static (List<User> Items, int Total) QueryUsers(int pageIndex, int pageSize, string name = "", string chineseName = "", string roleName = "")
        {
            if (pageSize <= 0)
                pageSize = 10;
            if (pageIndex < 0)
                pageIndex = 0;

            lock (_syncRoot)
            {
                var query = _users.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(name))
                    query = query.Where(t => (t.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(chineseName))
                    query = query.Where(t => (t.ChineseName ?? "").Contains(chineseName, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(roleName))
                    query = query.Where(t => string.Equals(t.RoleName, roleName, StringComparison.OrdinalIgnoreCase));

                var total = query.Count();
                var items = query
                    .OrderByDescending(t => t.Id)
                    .Skip(pageIndex * pageSize)
                    .Take(pageSize)
                    .Select(CloneUser)
                    .ToList();
                return (items, total);
            }
        }

        public static User SaveUser(User req)
        {
            if (req == null)
                return null;

            lock (_syncRoot)
            {
                if (req.Id <= 0)
                {
                    req.Id = _users.Count == 0 ? 1 : _users.Max(t => t.Id) + 1;
                    var created = NormalizeUser(req);
                    _users.Add(created);
                    return CloneUser(created);
                }

                var old = _users.FirstOrDefault(t => t.Id == req.Id);
                if (old == null)
                {
                    var created = NormalizeUser(req);
                    _users.Add(created);
                    return CloneUser(created);
                }

                var normalized = NormalizeUser(req);
                old.Name = normalized.Name;
                old.ChineseName = normalized.ChineseName;
                old.RoleName = normalized.RoleName;
                old.Age = normalized.Age;
                old.Score = normalized.Score;
                old.Birthday = normalized.Birthday;
                old.IsEnabled = normalized.IsEnabled;
                old.Gender = normalized.Gender;
                old.City = normalized.City;
                old.Types = normalized.Types;
                old.Description = normalized.Description;
                old.Photo = normalized.Photo;
                old.Photos = normalized.Photos;
                old.Icon = normalized.Icon;
                old.UserId = normalized.UserId;
                old.OrgId = normalized.OrgId;
                old.DeptId = normalized.DeptId;
                old.RoleIds = normalized.RoleIds;
                return CloneUser(old);
            }
        }

        public static int DeleteUsers(IEnumerable<long> ids)
        {
            if (ids == null)
                return 0;

            var idSet = ids.Where(t => t > 0).Distinct().ToHashSet();
            if (idSet.Count == 0)
                return 0;

            lock (_syncRoot)
            {
                var before = _users.Count;
                _users.RemoveAll(t => idSet.Contains(t.Id));
                return before - _users.Count;
            }
        }

        public static List<User> GetUsers(string roleName = "")
        {
            if (string.IsNullOrEmpty(roleName)) return _users;
            return _users.Where(u => u.RoleName == roleName).ToList();
        }

        private static User NormalizeUser(User src)
        {
            var roleIds = src.RoleIds?.Where(t => t > 0).Distinct().ToList() ?? new List<long>();
            var roleName = src.RoleName;
            if (roleIds.Count > 0)
            {
                var names = _roles
                    .Where(t => roleIds.Contains(t.Id))
                    .Select(t => t.Name)
                    .ToList();
                if (names.Count > 0)
                    roleName = string.Join(", ", names);
            }

            return new User
            {
                Id = src.Id,
                Name = src.Name,
                ChineseName = src.ChineseName,
                RoleName = roleName,
                Age = src.Age,
                Score = src.Score,
                Birthday = src.Birthday == default ? DateTime.Today : src.Birthday,
                IsEnabled = src.IsEnabled,
                Gender = src.Gender,
                City = src.City,
                Types = src.Types?.ToList() ?? new List<string>(),
                Description = src.Description,
                Photo = src.Photo,
                Photos = src.Photos?.ToList() ?? new List<string>(),
                Icon = src.Icon,
                UserId = src.UserId,
                OrgId = src.OrgId,
                DeptId = src.DeptId,
                RoleIds = roleIds
            };
        }

        private static User CloneUser(User src) => NormalizeUser(src);
    }

}
