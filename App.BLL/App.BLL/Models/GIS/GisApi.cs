using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using App.Components;
using App.Entities;
using App.Utils;

namespace App.DAL.GIS
{
    /// <summary>GIS 数据接口</summary>
    [UI("GIS", "GIS数据接口")]
    public class GisApi : EntityBase<GisApi>
    {
        [UI("菜单ID")] public long? MenuId { get; set; }
        [UI("名称")] public string Name { get; set; }
        [UI("排序")] public int SortId { get; set; }
        [UI("接口地址")] public string DataUrl { get; set; }
        [UI("最后数据量")] public int? DataCnt { get; set; }
        [UI("最后更新时间")] public DateTime? DataDt { get; set; }
        [UI("最近错误")] public string LastErr { get; set; }
        [UI("是否正常")] public bool IsLive { get; set; } = true;


        //
        public virtual GisMenu Menu { get; set; }
        public string MenuName => Menu?.Name;

        //
        public override object Export(ExportMode type = ExportMode.Normal)
        {
            return new
            {
                Id,
                MenuId,
                Name,
                DataUrl,
                DataCnt,
                DataDt,
                LastErr,
                IsLive,
                SortId,
                MenuName,
            };
        }

        public static IQueryable<GisApi> Search(long? menuId = null, string name = null)
        {
            var q = IncludeSet.AsQueryable();
            if (menuId.IsNotEmpty()) q = q.Where(t => t.MenuId == menuId.Value);
            if (name.IsNotEmpty()) q = q.Where(t => (t.Name ?? "").Contains(name.Trim()));
            return q;
        }

        /// <summary>刷新接口统计；返回（成功数, 失败数）</summary>
        public static (int okCount, int failCount) RefreshStats(long? menuId = null)
        {
            var q = Set.AsQueryable().Where(t => t.IsLive);
            if (menuId.IsNotEmpty())
            {
                var ids = GisMenu.All.GetDescendants(menuId).Select(t => t.Id).Distinct().ToList();
                q = q.Where(t => t.MenuId.HasValue && ids.Contains(t.MenuId.Value));
            }

            var items = q.ToList();
            var ok = 0;
            var fail = 0;
            var now = DateTime.Now;

            foreach (var api in items)
            {
                try
                {
                    var cnt = FetchDataCnt(api.DataUrl, null);
                    api.DataCnt = cnt;
                    api.DataDt = now;
                    api.LastErr = null;
                    ok++;
                }
                catch (Exception ex)
                {
                    api.LastErr = ex.Message?.Trim().TakeChars(500);
                    fail++;
                }
            }

            Db.SaveChanges();
            ClearCache();
            return (ok, fail);
        }

        static int FetchDataCnt(string dataUrl, object region)
        {
            if (dataUrl.IsEmpty())
                throw new Exception("DataUrl为空");

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var payload = region == null ? "{}" : JsonSerializer.Serialize(new { region });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var resp = client.PostAsync(dataUrl.Trim(), content).GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"HTTP {(int)resp.StatusCode}");

            var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (text.IsEmpty())
                return 0;

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.TryGetProperty("code", out var codeNode))
            {
                var code = codeNode.ValueKind == JsonValueKind.Number
                    ? codeNode.GetInt32()
                    : codeNode.GetValueType<int>(0);
                if (code != 0)
                {
                    var msg = root.TryGetProperty("message", out var msgNode)
                        ? msgNode.GetString()
                        : "接口返回code!=0";
                    throw new Exception(msg.IsEmpty() ? "接口返回失败" : msg);
                }
            }

            if (!root.TryGetProperty("data", out var dataNode))
                return 0;

            if (dataNode.ValueKind == JsonValueKind.Array)
                return dataNode.GetArrayLength();

            if (dataNode.ValueKind == JsonValueKind.Object)
            {
                if (dataNode.TryGetProperty("items", out var itemsNode) && itemsNode.ValueKind == JsonValueKind.Array)
                    return itemsNode.GetArrayLength();
                if (dataNode.TryGetProperty("list", out var listNode) && listNode.ValueKind == JsonValueKind.Array)
                    return listNode.GetArrayLength();
            }

            return 0;
        }
    }

    static class GisApiTextExt
    {
        public static string TakeChars(this string text, int maxLen)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            if (text.Length <= maxLen)
                return text;
            return text.Substring(0, maxLen);
        }

        public static T GetValueType<T>(this JsonElement node, T defaultValue)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(node.GetRawText());
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
