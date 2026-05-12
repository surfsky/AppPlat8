using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace App.Pages.AI
{
    public class VoiceTranscribeRequest
    {
        public IFormFile Audio { get; set; }
        public string Endpoint { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public string Language { get; set; }
        public string Prompt { get; set; }
        public double? Temperature { get; set; }
    }

    public class VoiceTranscribeResult
    {
        public bool Success { get; set; }
        public int Code { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public static class VoiceTranscribeHelper
    {
        public static async Task<VoiceTranscribeResult> TranscribeAsync(VoiceTranscribeRequest req)
        {
            if (req == null)
                return Fail(400, "参数错误");

            if (req.Audio == null || req.Audio.Length == 0)
                return Fail(400, "请上传音频文件");

            if (req.Audio.Length > 25 * 1024 * 1024)
                return Fail(400, "音频文件不能超过 25MB");

            if (string.IsNullOrWhiteSpace(req.Endpoint))
                return Fail(400, "请填写 API 地址");

            if (string.IsNullOrWhiteSpace(req.Model))
                return Fail(400, "请填写模型名称");

            var endpoint = BuildTranscriptionEndpoint(req.Endpoint);

            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
            using var form = new MultipartFormDataContent();

            await using var stream = req.Audio.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
                string.IsNullOrWhiteSpace(req.Audio.ContentType)
                    ? "application/octet-stream"
                    : req.Audio.ContentType
            );

            form.Add(fileContent, "file", req.Audio.FileName);
            form.Add(new StringContent(req.Model.Trim()), "model");

            if (!string.IsNullOrWhiteSpace(req.Language))
                form.Add(new StringContent(req.Language.Trim()), "language");

            if (!string.IsNullOrWhiteSpace(req.Prompt))
                form.Add(new StringContent(req.Prompt.Trim()), "prompt");

            if (req.Temperature != null)
                form.Add(new StringContent(req.Temperature.Value.ToString("0.0")), "temperature");

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = form };
            if (!string.IsNullOrWhiteSpace(req.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", req.ApiKey.Trim());

            HttpResponseMessage response;
            string body;
            try
            {
                response = await client.SendAsync(request);
                body = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return Fail(500, $"调用失败：{ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
            {
                return Fail(500, $"转写失败：{(int)response.StatusCode}", new { endpoint, body });
            }

            var text = ExtractText(body);
            return new VoiceTranscribeResult
            {
                Success = true,
                Code = 0,
                Message = "success",
                Data = new { text, raw = body, endpoint }
            };
        }

        private static string BuildTranscriptionEndpoint(string endpoint)
        {
            var url = (endpoint ?? string.Empty).Trim();
            if (url.EndsWith("/audio/transcriptions", StringComparison.OrdinalIgnoreCase))
                return url;

            url = url.TrimEnd('/');
            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return url + "/audio/transcriptions";

            return url + "/v1/audio/transcriptions";
        }

        private static string ExtractText(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("text", out var textNode))
                    return textNode.ToString();

                if (root.TryGetProperty("transcript", out var transcriptNode))
                    return transcriptNode.ToString();

                if (root.TryGetProperty("result", out var resultNode))
                    return resultNode.ToString();
            }
            catch
            {
                // ignore
            }

            return body;
        }

        private static VoiceTranscribeResult Fail(int code, string msg, object data = null)
        {
            return new VoiceTranscribeResult
            {
                Success = false,
                Code = code,
                Message = msg,
                Data = data
            };
        }
    }
}
