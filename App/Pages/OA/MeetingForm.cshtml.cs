using System;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.OA
{
    [Auth(Power.MeetingView)]
    public class MeetingFormModel : AdminModel
    {
        public Meeting Item { get; set; } = new Meeting();

        public void OnGet() { }

        public IActionResult OnGetData(long id)
        {
            var item = id > 0 ? Meeting.Get(id) : new Meeting { Day = DateTime.Today, Type = MeetingType.Other };
            return item == null ? BuildResult(404, "会议记录不存在") : BuildResult(0, "success", item.Export());
        }

        public IActionResult OnPostSave([FromBody] Meeting req)
        {
            if (req == null || req.Day == default || !req.OrgId.HasValue) return BuildResult(400, "请填写日期和科室");
            Meeting item;
            if (req.Id <= 0)
            {
                if (!CheckPower(Power.MeetingNew)) return BuildResult(403, "无权新增");
                item = new Meeting();
            }
            else
            {
                if (!CheckPower(Power.MeetingEdit)) return BuildResult(403, "无权修改");
                item = Meeting.Get(req.Id);
                if (item == null) return BuildResult(404, "会议记录不存在");
            }

            item.Day = req.Day.Date;
            item.Type = req.Type;
            item.OrgId = req.OrgId;
            item.Content = req.Content?.Trim();
            item.Image = Uploader.SaveFile(nameof(Meeting), req.Image);
            item.Save();
            return BuildResult(0, "保存成功", new { id = item.Id });
        }
    }
}