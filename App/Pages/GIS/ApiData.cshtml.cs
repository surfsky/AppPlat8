using System;
using System.Net.Http;
using App.Components;
using App.DAL;
using App.DAL.GIS;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.GIS
{
    [CheckPower(Power.GisGeometryView)]
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
                using var content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
                using var resp = client.PostAsync(url, content).GetAwaiter().GetResult();
                var text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!resp.IsSuccessStatusCode)
                {
                    ErrorText = $"HTTP {(int)resp.StatusCode}";
                    ResponseText = text;
                    return;
                }

                ResponseText = text;
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
            }
        }
    }
}
