using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using App.Components;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.EleUISamples
{
    public class TimerModel : BaseModel
    {
        private static int _refreshCount;

        public TimerRow Item { get; set; } = new();

        public IActionResult OnGetData(Paging pi, string name)
        {
            var refreshCount = Interlocked.Increment(ref _refreshCount);
            var refreshedAt = DateTime.Now;
            var rows = Enumerable.Range(1, 24)
                .Select(id => new TimerRow
                {
                    Id = id,
                    Name = $"示例任务 {id:D2}",
                    Status = (id + refreshCount) % 3 == 0 ? "处理中" : "已就绪",
                    RefreshCount = refreshCount,
                    RefreshedAt = refreshedAt
                });

            if (!string.IsNullOrWhiteSpace(name))
                rows = rows.Where(row => row.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

            var filtered = rows.ToList();
            var pageIndex = Math.Max(0, pi?.PageIndex ?? 0);
            var pageSize = pi?.PageSize > 0 ? pi.PageSize : 10;
            var items = filtered.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            return BuildResult(0, "success", new
            {
                items,
                total = filtered.Count
            });
        }
    }

    public class TimerRow
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public int RefreshCount { get; set; }
        public DateTime RefreshedAt { get; set; }
    }
}
