using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [Auth(Power.GisGeometryView)]
    public class ApiDataModel : AdminModel
    {
        public long ApiId { get; set; }
        public string ApiName { get; set; } = string.Empty;
        public string DataUrl { get; set; } = string.Empty;
        public string ResponseText { get; set; } = string.Empty;
        public string ErrorText { get; set; } = string.Empty;

        public void OnGet(long? apiId, string dataUrl)
        {
            ApiId = apiId ?? 0;

            if (!string.IsNullOrWhiteSpace(dataUrl))
            {
                DataUrl = dataUrl.Trim();
                ApiName = "临时地址";
                LoadData(DataUrl);
                return;
            }

            if (ApiId <= 0)
                return;

            var api = GisApi.Get(ApiId);
            if (api == null)
            {
                ErrorText = "接口不存在";
                return;
            }

            ApiName = api.Name ?? string.Empty;
            DataUrl = api.DataUrl ?? string.Empty;
            LoadData(DataUrl);
        }

        private void LoadData(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                ErrorText = "DataUrl为空";
                return;
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

                // HttpApi 默认按表单参数解析，优先使用 form-urlencoded 触发 POST。
                using var formContent = new FormUrlEncodedContent(new Dictionary<string, string>());
                using var postResp = client.PostAsync(url, formContent).GetAwaiter().GetResult();
                var postText = postResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (postResp.IsSuccessStatusCode)
                {
                    if (!IsBusinessSuccess(postText, out var postErr))
                    {
                        ErrorText = postErr;
                        ResponseText = postText;
                        return;
                    }

                    ResponseText = postText;
                    SaveSuccessResult(ApiId, postText);
                    return;
                }

                // 某些接口可能只支持 GET，作为回退方案便于调试联通性。
                using var getResp = client.GetAsync(url).GetAwaiter().GetResult();
                var getText = getResp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (getResp.IsSuccessStatusCode)
                {
                    if (!IsBusinessSuccess(getText, out var getErr))
                    {
                        ErrorText = getErr;
                        ResponseText = getText;
                        return;
                    }

                    ResponseText = getText;
                    SaveSuccessResult(ApiId, getText);
                    return;
                }

                ErrorText = $"POST HTTP {(int)postResp.StatusCode}; GET HTTP {(int)getResp.StatusCode}";
                ResponseText = $"[POST]\n{postText}\n\n[GET]\n{getText}";
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
            }
        }

        private static void SaveSuccessResult(long apiId, string responseText)
        {
            if (apiId <= 0)
                return;

            var api = GisApi.Get(apiId);
            if (api == null)
                return;

            api.DataCnt = ParseDataCount(responseText);
            api.DataDt = DateTime.Now;
            api.LastErr = null;
            api.Save();

            GisMenu.FixAll();
        }

        private static bool IsBusinessSuccess(string responseText, out string err)
        {
            err = string.Empty;
            if (string.IsNullOrWhiteSpace(responseText))
                return true;

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return true;
                if (!root.TryGetProperty("code", out var codeNode))
                    return true;

                var code = codeNode.ValueKind == JsonValueKind.Number
                    ? codeNode.GetInt32()
                    : int.TryParse(codeNode.GetString(), out var parsed) ? parsed : 0;

                if (code == 0)
                    return true;

                err = TryGetText(root, "message", "msg", "info") ?? $"接口返回失败，code={code}";
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static int ParseDataCount(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return 0;

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return 0;
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
            }
            catch
            {
            }

            return 0;
        }

        private static string TryGetText(JsonElement root, params string[] names)
        {
            foreach (var name in names)
            {
                if (!root.TryGetProperty(name, out var node))
                    continue;
                if (node.ValueKind == JsonValueKind.String)
                    return node.GetString();
                if (node.ValueKind == JsonValueKind.Number || node.ValueKind == JsonValueKind.True || node.ValueKind == JsonValueKind.False)
                    return node.ToString();
            }

            return null;
        }
    }
}
