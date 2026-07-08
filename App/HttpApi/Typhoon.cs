using System;
using System.Linq;
using App.DAL.GIS;
using App.HttpApi;

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
    }
}
