using System;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.Utils;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
    public class TyphoonsModel : AdminModel
    {
        public TyphoonGridItem Item { get; set; }

        /// <summary>初始化</summary>
        public void OnGet()
        {
            Item = new TyphoonGridItem();
        }

        /// <summary>获取列表</summary>
        public IActionResult OnGetData(Paging pi, int? year, string name, string code)
        {
            if (pi.SortField.IsEmpty())
            {
                pi.SortField = "Code";
                pi.SortDirection = "DESC";
            }

            var tyList = GisTyphoon.Search(name: name, code: code, year: year).ToList();
            var codes = tyList.Select(t => t.Code).Where(t => t.IsNotEmpty()).Distinct().ToList();
            var statMap = GisTyphoonLog.ValidSet
                .Where(t => codes.Contains(t.Code))
                .GroupBy(t => t.Code)
                .Select(t => new
                {
                    Code = t.Key,
                    LogCnt = t.Count(),
                    StartUtc = t.Min(x => x.TimeUtc),
                    EndUtc = t.Max(x => x.TimeUtc)
                })
                .ToDictionary(t => t.Code, t => t);

            var rows = tyList.Select(t =>
            {
                statMap.TryGetValue(t.Code, out var stat);
                return new TyphoonGridItem
                {
                    Id = t.Id,
                    Code = t.Code,
                    Year = t.Year,
                    Name = t.Name,
                    ChineseName = t.ChineseName,
                    BirthUtc = t.BirthUtc ?? stat?.StartUtc,
                    DeathUtc = t.DeathUtc ?? stat?.EndUtc,
                    MaxLevel = t.MaxLevel,
                    IsLand = t.IsLand,
                    LogCnt = stat?.LogCnt ?? 0,
                    LastTimeUtc = stat?.EndUtc
                };
            });
            var list = rows.AsQueryable().SortPageExport(pi);
            return BuildResult(0, "success", list, pi);
        }
    }

    /// <summary>台风表格行</summary>
    public class TyphoonGridItem : IExport
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public int? Year { get; set; }
        public string Name { get; set; }
        public string ChineseName { get; set; }
        public DateTime? BirthUtc { get; set; }
        public DateTime? DeathUtc { get; set; }
        public int? MaxLevel { get; set; }
        public bool? IsLand { get; set; }
        public int LogCnt { get; set; }
        public DateTime? LastTimeUtc { get; set; }
        public string IsLandText => IsLand == true ? "是" : "否";

        /// <summary>导出对象</summary>
        public object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                Code,
                Year,
                Name,
                ChineseName,
                BirthUtc,
                DeathUtc,
                MaxLevel,
                IsLand,
                IsLandText,
                LogCnt,
                LastTimeUtc
            };
        }
    }
}
