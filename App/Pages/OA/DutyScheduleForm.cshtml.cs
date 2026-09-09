using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.Entities;
using App.Utils;
using App.EleUI;

namespace App.Pages.OA
{
    public class DutyScheduleFormModel : AdminModel
    {
        public DutySchedule Item { get; set; } = new DutySchedule();

        public void OnGet() { }

        /// <summary>表单数据读取</summary>
        public IActionResult OnGetData(long id)
        {
            var item = id > 0 ? DutySchedule.Get(id) : new DutySchedule { Day = DateTime.Today };
            return BuildResult(0, "success", item.Export());
        }

        /// <summary>保存</summary>
        public IActionResult OnPostSave([FromBody] DutySchedule req)
        {
            if (req == null) return BuildResult(400, "参数错误");
            DutySchedule item;
            if (req.Id <= 0)
            {
                if (!CheckPower(Power.DutyScheduleNew))  return BuildResult(403, "无权新增");
                item = new DutySchedule();
            }
            else
            {
                if (!CheckPower(Power.DutyScheduleEdit)) return BuildResult(403, "无权编辑");
                item = DutySchedule.Get(req.Id);
                if (item == null) return BuildResult(404, $"未找到 Id={req.Id} 的值班记录");
            }

            item.Day      = req.Day.Date;
            item.Leader   = req.Leader?.Trim();
            item.Chief    = req.Chief?.Trim();
            item.Member1  = req.Member1?.Trim();
            item.Member2  = req.Member2?.Trim();
            item.Member3  = req.Member3?.Trim();
            item.Member4  = req.Member4?.Trim();
            item.Member5  = req.Member5?.Trim();
            item.Member6  = req.Member6?.Trim();
            item.Member7  = req.Member7?.Trim();
            item.Member8  = req.Member8?.Trim();
            item.Member9  = req.Member9?.Trim();
            item.Member10 = req.Member10?.Trim();
            item.Member11 = req.Member11?.Trim();
            item.Member12 = req.Member12?.Trim();
            item.Member13 = req.Member13?.Trim();
            item.Member14 = req.Member14?.Trim();
            item.Member15 = req.Member15?.Trim();

            // 唯一性校验：同一天只能有 1 条
            var dup = DutySchedule.Set.FirstOrDefault(d => d.Day == item.Day && d.Id != item.Id);
            if (dup != null)
                return BuildResult(409, $"日期 {item.Day:yyyy-MM-dd} 已存在值班记录（Id={dup.Id}），请勿重复");

            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
