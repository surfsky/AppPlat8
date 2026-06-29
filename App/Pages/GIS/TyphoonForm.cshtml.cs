using System;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryEdit)]
    public class TyphoonFormModel : AdminModel
    {
        public GisTyphoon Item { get; set; }

        /// <summary>初始化</summary>
        public void OnGet()
        {
        }

        /// <summary>获取台风数据</summary>
        public IActionResult OnGetData(long id)
        {
            var item = GisTyphoon.GetDetail(id) ?? new GisTyphoon();
            return BuildResult(0, "success", item.Export());
        }

        /// <summary>保存台风</summary>
        public IActionResult OnPostSave([FromBody] GisTyphoon req)
        {
            if (req == null)
                return BuildResult(400, "参数错误");
            if (req.Code.IsEmpty())
                return BuildResult(400, "编号不能为空");

            GisTyphoon item;
            if (req.Id > 0)
            {
                item = GisTyphoon.Get(req.Id);
                if (item == null)
                    return BuildResult(404, "数据不存在");
            }
            else
            {
                item = new GisTyphoon
                {
                    CreateDt = DateTime.Now,
                    CreatorId = GetUserId()
                };
            }

            item.Code = req.Code.Trim();
            item.Name = req.Name?.Trim();
            item.ChineseName = req.ChineseName?.Trim();
            item.MaxLevel = req.MaxLevel;
            item.IsLand = req.IsLand ?? false;
            item.BirthUtc = req.BirthUtc;
            item.DeathUtc = req.DeathUtc;
            item.Save();

            return BuildResult(0, "保存成功", new { id = item.Id });
        }
    }
}
