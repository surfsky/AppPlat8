using System;
using System.Collections.Generic;
using System.Linq;
using App.DAL;

namespace App.Pages.Me
{
    public class WorkDeskModel : AdminModel
    {
        public string SiteTitle { get; set; }
        public List<WorkDeskSummaryCard> SummaryCards { get; set; } = new();
        public List<WorkDeskEntryCard> EntryCards { get; set; } = new();
        public List<WorkDeskTaskBoard> TaskBoards { get; set; } = new();

        /// <summary>初始化工作台展示数据。</summary>
        public void OnGet()
        {
            SiteTitle = SiteConfig.Instance.Title;
            EntryCards = BuildEntryCards();
            TaskBoards = BuildTaskBoards();
            SummaryCards = BuildSummaryCards(TaskBoards);
        }

        /// <summary>构建顶部摘要卡片。</summary>
        private static List<WorkDeskSummaryCard> BuildSummaryCards(List<WorkDeskTaskBoard> boards)
        {
            var allTasks = (boards ?? new List<WorkDeskTaskBoard>()).SelectMany(t => t.Tasks ?? new List<WorkDeskTaskItem>()).ToList();
            var avgProgress = allTasks.Count == 0 ? 0 : (int)Math.Round(allTasks.Average(t => t.Progress));
            var totalCount = allTasks.Count;
            var inProgressCount = allTasks.Count(t => string.Equals(t.Status, "进行中", StringComparison.OrdinalIgnoreCase));
            var completedCount = allTasks.Count(t => string.Equals(t.Status, "已完成", StringComparison.OrdinalIgnoreCase));

            return new List<WorkDeskSummaryCard>
            {
                new() { Title = "任务总数", Value = totalCount.ToString(), Remark = "演示任务面板合计" },
                new() { Title = "平均进度", Value = $"{avgProgress}%", Remark = "按当前示例数据计算" },
                new() { Title = "进行中", Value = inProgressCount.ToString(), Remark = "持续跟进事项" },
                new() { Title = "已完成", Value = completedCount.ToString(), Remark = "已进入收尾阶段" },
            };
        }

        /// <summary>构建系统入口卡片。</summary>
        private static List<WorkDeskEntryCard> BuildEntryCards()
        {
            return new List<WorkDeskEntryCard>
            {
                new()
                {
                    Title = "应急一张图",
                    Badge = "GIS",
                    Description = "进入全局应急一张图地图，查看图层、点位、场景面板和空间信息。",
                    Url = "/GIS/Index",
                    Target = "_blank",
                    Icon = "fas fa-earth-asia",
                    IconBg = "bg-gradient-to-br from-sky-500 to-blue-600",
                    Stats = new List<WorkDeskEntryStat>
                    {
                        new() { Title = "入口地址", Value = "/GIS/Index" },
                        new() { Title = "适用场景", Value = "地图浏览与态势分析" },
                    }
                },
                new()
                {
                    Title = "知识库",
                    Badge = "KB",
                    Description = "进入知识库导航，快速打开目录、文档和资料沉淀页面。",
                    Url = "/KB/Index",
                    Icon = "fas fa-book-open-reader",
                    IconBg = "bg-gradient-to-br from-violet-500 to-fuchsia-600",
                    Stats = new List<WorkDeskEntryStat>
                    {
                        new() { Title = "入口地址", Value = "/KB/Index" },
                        new() { Title = "适用场景", Value = "资料检索与文档学习" },
                    }
                },
                new()
                {
                    Title = "通讯录",
                    Badge = "CRM",
                    Description = "检索应急通讯录。",
                    Url = "/CRM/Contacts",
                    Icon = "fas fa-book-open-reader",
                    IconBg = "bg-gradient-to-br from-violet-500 to-fuchsia-600",
                },
                new()
                {
                    Title = "值班表",
                    Badge = "DUTY",
                    Description = "查看近期值班表。",
                    Url = "/Duty/Index",
                    Icon = "fas fa-book-open-reader",
                    IconBg = "bg-gradient-to-br from-violet-500 to-fuchsia-600",
                },
            };
        }

        /// <summary>构建演示任务看板数据。</summary>
        private static List<WorkDeskTaskBoard> BuildTaskBoards()
        {
            var boards = new List<WorkDeskTaskBoard>
            {
                new()
                {
                    Title = "交办任务",
                    Description = "领导交办和专项推进事项",
                    Icon = "fas fa-list-check",
                    IconBg = "bg-gradient-to-br from-blue-500 to-cyan-500",
                    Tasks = new List<WorkDeskTaskItem>
                    {
                        CreateTask("台风防御演练方案上报", "整理演练流程与附件材料，等待局办审核。", "徐建泽", "进行中", 78, DateTime.Today.AddDays(3), "/Tasks/ToDo"),
                        CreateTask("防汛仓库设备清单复核", "核对库存数量与领用记录，补齐缺失设备照片。", "陈晓燕", "进行中", 55, DateTime.Today.AddDays(6), "/Tasks/ToDo"),
                        CreateTask("应急值守月报归档", "已完成归档与签批流转。", "张瑞", "已完成", 100, DateTime.Today.AddDays(-1), "/Tasks/ToDo"),
                    }
                },
                new()
                {
                    Title = "隐患排查任务",
                    Description = "检查对象、隐患处理和复查事项",
                    Icon = "fas fa-triangle-exclamation",
                    IconBg = "bg-gradient-to-br from-amber-500 to-orange-500",
                    Tasks = new List<WorkDeskTaskItem>
                    {
                        CreateTask("危化企业专项排查", "重点核查储罐区和消防设施，待补录整改照片。", "林志恒", "进行中", 64, DateTime.Today.AddDays(2), "/Checks/CheckTasks"),
                        CreateTask("老旧厂房安全复查", "针对上次发现的电气线路问题开展复查。", "王梦洁", "待开始", 20, DateTime.Today.AddDays(8), "/Checks/CheckTasks"),
                        CreateTask("校园周边燃气隐患核验", "已完成现场核验，待形成闭环报告。", "周晓峰", "已完成", 100, DateTime.Today.AddDays(-2), "/Checks/CheckTasks"),
                    }
                },
                new()
                {
                    Title = "科室任务",
                    Description = "科室内部协同事项与周计划",
                    Icon = "fas fa-users-gear",
                    IconBg = "bg-gradient-to-br from-emerald-500 to-teal-500",
                    Tasks = new List<WorkDeskTaskItem>
                    {
                        CreateTask("八月份值班表发布", "完成科室排班汇总并同步到共享目录。", "孙宁", "已完成", 100, DateTime.Today.AddDays(-4), "/Duty/Index"),
                        CreateTask("应急预案修订意见汇总", "收集各条线反馈，形成修订对照稿。", "黄诗雅", "进行中", 72, DateTime.Today.AddDays(5), "/Me/WorkDesk"),
                        CreateTask("视频会议设备巡检", "核对会议室音视频设备状态，安排缺陷报修。", "郑豪", "待开始", 15, DateTime.Today.AddDays(10), "/Me/WorkDesk"),
                    }
                }
            };

            foreach (var board in boards)
                board.RefreshStats();
            return boards;
        }

        /// <summary>创建单条任务演示数据。</summary>
        private static WorkDeskTaskItem CreateTask(string title, string summary, string owner, string status, int progress, DateTime dueDate, string url = "", string target = "_self")
        {
            var safeProgress = Math.Max(0, Math.Min(100, progress));
            var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "进行中" : status.Trim();
            var statusClass = normalizedStatus switch
            {
                "已完成" => "bg-emerald-100 text-emerald-700",
                "待开始" => "bg-slate-200 text-slate-600",
                _ => "bg-amber-100 text-amber-700",
            };
            //var progressBarClass = normalizedStatus switch
            //{
            //    "已完成" => "bg-emerald-500",
            //    "待开始" => "bg-slate-400",
            //    _ => "bg-gradient-to-r from-blue-500 to-cyan-500",
            //};
            var progressBarClass = normalizedStatus switch
            {
                "已完成" => "bg-emerald-500",
                "待开始" => "bg-emerald-500",
                _ => "bg-emerald-500",
            };

            return new WorkDeskTaskItem
            {
                Title = title,
                Summary = summary,
                Owner = owner,
                Status = normalizedStatus,
                Progress = safeProgress,
                DueDate = dueDate,
                DueDateText = dueDate.ToString("yyyy-MM-dd"),
                StatusClass = statusClass,
                ProgressBarClass = progressBarClass,
                Url = url,
                Target = string.IsNullOrWhiteSpace(target) ? "_self" : target,
            };
        }
    }

    public class WorkDeskSummaryCard
    {
        public string Title { get; set; }
        public string Value { get; set; }
        public string Remark { get; set; }
    }

    public class WorkDeskEntryCard
    {
        public string Title { get; set; }
        public string Badge { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public string Target { get; set; } = "_self";
        public string Icon { get; set; }
        public string IconBg { get; set; }
        public List<WorkDeskEntryStat> Stats { get; set; } = new();
    }

    public class WorkDeskEntryStat
    {
        public string Title { get; set; }
        public string Value { get; set; }
    }

    public class WorkDeskTaskBoard
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string IconBg { get; set; }
        public List<WorkDeskTaskItem> Tasks { get; set; } = new();
        public int AverageProgress { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }

        /// <summary>刷新看板统计值。</summary>
        public void RefreshStats()
        {
            var items = Tasks ?? new List<WorkDeskTaskItem>();
            AverageProgress = items.Count == 0 ? 0 : (int)Math.Round(items.Average(t => t.Progress));
            InProgressCount = items.Count(t => string.Equals(t.Status, "进行中", StringComparison.OrdinalIgnoreCase));
            CompletedCount = items.Count(t => string.Equals(t.Status, "已完成", StringComparison.OrdinalIgnoreCase));
        }
    }

    public class WorkDeskTaskItem
    {
        public string Title { get; set; }
        public string Summary { get; set; }
        public string Owner { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public DateTime DueDate { get; set; }
        public string DueDateText { get; set; }
        public string StatusClass { get; set; }
        public string ProgressBarClass { get; set; }
        public string Url { get; set; }
        public string Target { get; set; } = "_self";
    }
}
