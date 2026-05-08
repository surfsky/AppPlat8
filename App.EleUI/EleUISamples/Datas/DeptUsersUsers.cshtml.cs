using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUISamples
{
    public class DeptUsersUsersModel : BaseModel
    {
        private static readonly long[] LeafDeptIds = { 110, 120, 210, 220, 310, 320 };

        private static readonly Dictionary<long, string> DeptNames = new()
        {
            [110] = "行政办公室",
            [120] = "人力资源部",
            [210] = "平台研发部",
            [220] = "应用研发部",
            [310] = "客户成功部",
            [320] = "市场运营部"
        };

        private static readonly Dictionary<long, long[]> DeptScopes = new()
        {
            [100] = new[] { 110L, 120L },
            [200] = new[] { 210L, 220L },
            [300] = new[] { 310L, 320L },
            [110] = new[] { 110L },
            [120] = new[] { 120L },
            [210] = new[] { 210L },
            [220] = new[] { 220L },
            [310] = new[] { 310L },
            [320] = new[] { 320L }
        };

        public DeptUserListItem Item { get; set; } = new();
        public long DeptId { get; set; }
        public string DeptName { get; set; } = string.Empty;

        public void OnGet(long deptId = 0)
        {
            DeptId = deptId;
            DeptName = ResolveDeptName(deptId);
        }

        public IActionResult OnGetData(Paging pi, long deptId = 0, string name = "", string chineseName = "")
        {
            List<User> users = (Data.GetUsers() ?? new List<User>()).ToList();

            if (!string.IsNullOrWhiteSpace(name))
                users = users.Where(t => (t.Name ?? string.Empty).Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(chineseName))
                users = users.Where(t => (t.ChineseName ?? string.Empty).Contains(chineseName, StringComparison.OrdinalIgnoreCase)).ToList();

            var scope = ResolveDeptScope(deptId);
            if (scope.Count > 0)
                users = users.Where(t => scope.Contains(GetDeptIdByUser(t))).ToList();

            var total = users.Count;
            var pageIndex = pi?.PageIndex ?? 0;
            var requestedPageSize = pi?.PageSize ?? 10;
            var pageSize = requestedPageSize <= 0 ? 10 : requestedPageSize;

            var items = users
                .OrderByDescending(t => t.Id)
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .Select(t => new DeptUserListItem
                {
                    Id = t.Id,
                    Name = t.Name,
                    ChineseName = t.ChineseName,
                    RoleName = t.RoleName,
                    DeptName = ResolveDeptName(GetDeptIdByUser(t))
                })
                .ToList();

            return BuildResult(0, "success", new
            {
                items,
                total
            });
        }

        private static long GetDeptIdByUser(User user)
        {
            if (user == null)
                return 110;

            var explicitDeptId = user.DeptId ?? 0;
            if (DeptNames.ContainsKey(explicitDeptId))
                return explicitDeptId;

            return GetFallbackDeptId(user.Id);
        }

        private static long GetFallbackDeptId(long userId)
        {
            if (userId <= 0)
                return 110;

            return LeafDeptIds[(int)((userId - 1) % LeafDeptIds.Length)];
        }

        private static HashSet<long> ResolveDeptScope(long deptId)
        {
            if (deptId <= 0)
                return new HashSet<long>();

            if (DeptScopes.TryGetValue(deptId, out var scoped))
                return scoped.ToHashSet();

            return new HashSet<long> { deptId };
        }

        private static string ResolveDeptName(long deptId)
        {
            if (deptId <= 0)
                return string.Empty;

            if (deptId == 100)
                return "综合管理部";
            if (deptId == 200)
                return "研发中心";
            if (deptId == 300)
                return "运营中心";

            return DeptNames.TryGetValue(deptId, out var name) ? name : $"部门{deptId}";
        }
    }

    public class DeptUserListItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ChineseName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string DeptName { get; set; } = string.Empty;
    }
}
