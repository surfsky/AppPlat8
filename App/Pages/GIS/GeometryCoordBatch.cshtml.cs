using System;
using System.Collections.Generic;
using System.Linq;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.Utils.Gis;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryEdit)]
    public class GeometryCoordBatchModel : AdminModel
    {
        public List<long> SelectedIds { get; set; } = new();
        public string DefaultCoordType { get; set; } = GisHelper.CoordTypeWgs84;

        public void OnGet(string ids = null)
        {
            SelectedIds = ParseIds(ids);
        }

        public IActionResult OnPostConvert([FromBody] CoordBatchConvertReq req)
        {
            var logs = new List<string>();
            var ids = req?.Ids?.Where(x => x > 0).Distinct().ToList() ?? new List<long>();
            if (ids.Count == 0)
                return BuildResult(400, "请先选择要处理的点位", new { success = 0, fail = 0, logs });

            var coordType = GisHelper.NormalizeCoordType(req?.CoordType);
            var success = 0;
            var fail = 0;
            logs.Add($"开始转换，共 {ids.Count} 条，源坐标系: {coordType}，目标坐标系: {GisHelper.CoordTypeWgs84}");

            foreach (var id in ids)
            {
                try
                {
                    var item = GisGeometry.Get(id);
                    if (item == null)
                        throw new Exception($"ID={id} 的点位不存在");
                    if (item.Type != GeometryType.Point)
                        throw new Exception($"[{item.Name}] 不是“点”类型");
                    if (string.IsNullOrWhiteSpace(item.Gps))
                        throw new Exception($"[{item.Name}] 缺少经纬度");

                    if (!GisHelper.TryConvertToWgs84(item.Gps, coordType, out var gpsWgs84, out var error))
                        throw new Exception(error);

                    item.Gps = gpsWgs84;
                    item.Save();
                    success++;
                    logs.Add($"[{item.Name}] 转换成功 -> {gpsWgs84}");
                }
                catch (Exception ex)
                {
                    fail++;
                    logs.Add($"ID={id} 转换失败: {ex.Message}");
                }
            }

            var msg = fail == 0
                ? $"转换完成，成功{success}条"
                : $"转换完成，成功{success}条，失败{fail}条";

            return BuildResult(0, msg, new
            {
                total = success + fail,
                success,
                fail,
                logs
            });
        }

        private static List<long> ParseIds(string ids)
        {
            var list = new List<long>();
            if (string.IsNullOrWhiteSpace(ids))
                return list;

            var parts = ids
                .Replace("，", ",")
                .Replace("；", ",")
                .Replace(";", ",")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                if (long.TryParse(part, out var id) && id > 0)
                    list.Add(id);
            }

            return list.Distinct().ToList();
        }

        public class CoordBatchConvertReq
        {
            public List<long> Ids { get; set; } = new();
            public string CoordType { get; set; }
        }
    }
}
