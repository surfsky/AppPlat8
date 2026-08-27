using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using App.DAL;
using App.DAL.GIS;
using App.Entities;
using App.HttpApi;
using App.Utils;
using App.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace App.API
{
    /// <summary>台风数据接口</summary>
    public class Typhoon
    {
        /// <summary>列出台风</summary>
        [HttpApi("台风列表", AuthLogin = false)]
        public static APIResult List(int? year = null, string name = null)
        {
            var list = GisTyphoon.Search(name: name, year: year).ToList();
            var codes = list.Select(t => t.Code).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().ToList();
            var cntMap = GisTyphoonLog.ValidSet
                .Where(t => codes.Contains(t.Code))
                .GroupBy(t => t.Code)
                .Select(t => new { t.Key, Cnt = t.Count() })
                .ToDictionary(t => t.Key, t => t.Cnt);

            var data = list.Select(t => new
            {
                t.Id,
                t.Code,
                t.Name,
                t.ChineseName,
                t.BirthUtc,
                t.DeathUtc,
                t.MaxLevel,
                t.IsLand,
                t.Year,
                logCnt = cntMap.TryGetValue(t.Code, out var cnt) ? cnt : 0
            });
            return data.ToResult();
        }

        /// <summary>获取台风</summary>
        [HttpApi("台风详情", AuthLogin = false)]
        public static APIResult Get(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new APIResult(400, "缺少台风编号");

            var item = GisTyphoon.Search(code: code.Trim()).FirstOrDefault(t => t.Code == code.Trim());
            if (item == null)
                return new APIResult(404, "未找到台风");

            var logCnt = GisTyphoonLog.ValidSet.Count(t => t.Code == item.Code);
            return new
            {
                item.Id,
                item.Code,
                item.Name,
                item.ChineseName,
                item.BirthUtc,
                item.DeathUtc,
                item.MaxLevel,
                item.IsLand,
                item.Year,
                item.DisplayName,
                logCnt
            }.ToResult();
        }

        /// <summary>获取轨迹</summary>
        [HttpApi("台风轨迹", AuthLogin = false)]
        public static APIResult Logs(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new APIResult(400, "缺少台风编号");
            var data = GisTyphoonLog.Search(code: code.Trim()).ToList();
            return data.ToResult();
        }

        /// <summary>获取预测</summary>
        [HttpApi("台风预测", AuthLogin = false)]
        public static APIResult Predict(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new APIResult(400, "缺少台风编号");
            return new APIResult(0, "暂无预测数据", Array.Empty<object>());
        }

        /// <summary>导入前端从实时源抓取的台风数据（自动更新/新增入库）</summary>
        [HttpApi("导入实时台风", AuthLogin = false)]
        public static APIResult ImportLive()
        {
            string body;
            try
            {
                var req = Asp.Request;
                if (req == null)
                    return new APIResult(400, "无法获取当前请求上下文");
                using var sr = new StreamReader(req.Body);
                body = sr.ReadToEnd();
            }
            catch
            {
                return new APIResult(400, "无法读取请求体");
            }
            if (string.IsNullOrWhiteSpace(body))
                return new APIResult(400, "请求体为空");
            JsonElement dataEl = default;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out dataEl))
                {
                    body = dataEl.GetRawText();
                }
                else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("payload", out dataEl))
                {
                    body = dataEl.GetRawText();
                }
            }
            catch { }

            using var db = EntityConfig.Db as AppPlatContext;
            var res = GisTyphoonImporter.ImportLiveData(db, body);
            return new APIResult(0,
                $"导入完成：新增{res.TyphoonAddCnt}个台风，更新{res.TyphoonEditCnt}个",
                new
                {
                    res.TyphoonAddCnt,
                    res.TyphoonEditCnt,
                    res.LogAddCnt,
                    res.LogDeleteCnt,
                    res.FileCnt,
                    logs = res.Logs.Take(50).ToList()
                });
        }
    }
}
