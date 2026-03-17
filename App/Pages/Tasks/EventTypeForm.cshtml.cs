using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.DAL.OA;
using App.HttpApi;
using App.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace App.Pages.OA
{
    [CheckPower(Power.EventEdit)]
    public class EventTypeFormModel : AdminModel
    {
        public EventType Item { get; set; }

        public void OnGet() { }

        public IActionResult OnGetData(long id)
        {
            var item = EventType.GetDetail(id) ?? new EventType();
            return BuildResult(0, "success", item);
        }

        public IActionResult OnPostSave([FromBody] EventType req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");

            var item = req.Id > 0 ? EventType.Get(req.Id) : new EventType();
            item.Name = req.Name;
            item.Save();
            return BuildResult(0, "保存成功");
        }
    }
}
