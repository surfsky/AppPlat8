using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.Entities;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.Me
{
    //=========================================================================
    // 页面 DTO
    //=========================================================================
    /// <summary>工作台链接</summary>
    public class WorkDeskLink
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Target { get; set; } = "_self";
        public string IconCls { get; set; }
    }


    /// <summary>工作台统计卡片</summary>
    public class WorkDeskStatCard
    {
        public string Title { get; set; }
        public int Count { get; set; }
        public string Url { get; set; }
        public bool ShowBadge => Count > 0;
        public string IconCls { get; set; }

        public WorkDeskStatCard() {}
        public WorkDeskStatCard(string title, int count, string url, string iconCls)
        {
            Title = title;
            Count = count;
            Url = url;
            IconCls = iconCls;
        }
    }

    /// <summary>工作台任务行（被 WorkDesk / WorkDeskOrg 共用）</summary>
    public class WorkDeskTaskRow : IExport
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string LevelText { get; set; }
        public string CreatorText { get; set; }
        public string CreatorOrg { get; set; }
        public int Progress { get; set; }
        public string ProgressText { get; set; }
        public DateTime? StartDt { get; set; }
        public DateTime? ExpireDt { get; set; }
        public string DetailUrl { get; set; }
        public long? CheckerId { get; set; }

        public object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id, Name, LevelText,
                CreatorText, CreatorOrg,
                Progress, ProgressText,
                StartDt, ExpireDt,
                DetailUrl, CheckerId,
            };
        }
    }

    /// <summary>工作台任务对象行（CheckTaskObject）</summary>
    public class WorkDeskTaskObjectRow : IExport
    {
        public long Id { get; set; }
        public long? TaskId { get; set; }
        public string TaskName { get; set; }
        public long? CheckObjectId { get; set; }
        public string ObjectName { get; set; }
        public string DutyOrgName { get; set; }
        public string CheckerName { get; set; }
        public bool IsFinished { get; set; }
        public string StatusName => IsFinished ? "已完成" : "未完成";
        public DateTime? TaskStartDt { get; set; }
        public DateTime? TaskExpireDt { get; set; }
        public string DetailUrl { get; set; }

        public object Export(ExportMode mode = ExportMode.Normal)
        {
            return new
            {
                Id, TaskId, TaskName, CheckObjectId, ObjectName,
                DutyOrgName, CheckerName, IsFinished, StatusName,
                TaskStartDt, TaskExpireDt, DetailUrl,
            };
        }
    }

}
