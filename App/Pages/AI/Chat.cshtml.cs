using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace App.Pages.AI
{
    [IgnoreAntiforgeryToken]
    [CheckPower(Power.AIChat)]
    public class ChatModel : AdminModel
    {
        public List<AIConfigOption> Configs { get; set; } = new();
        public long? DefaultConfigId { get; set; }

        public void OnGet()
        {
            var list = AIConfig.Search(null, true).ToList();
            Configs = list.Select(t => new AIConfigOption
            {
                Id = t.Id,
                Name = t.Name,
                Model = t.Model
            }).ToList();
            DefaultConfigId = list.OrderByDescending(t => t.IsDefault).ThenBy(t => t.SortId).ThenBy(t => t.Id).FirstOrDefault()?.Id;
        }

        public async Task<IActionResult> OnPostSend([FromBody] ChatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Message))
                return BuildResult(400, "消息不能为空");

            var cfg = req.ConfigId > 0 ? AIConfig.Get(req.ConfigId) : AIConfig.GetDefault();
            if (cfg == null)
                return BuildResult(400, "请先配置可用的AI模型");
            if (!cfg.InUsed)
                return BuildResult(400, "当前配置已禁用");
            if (string.IsNullOrWhiteSpace(cfg.BaseUrl) || string.IsNullOrWhiteSpace(cfg.Model))
                return BuildResult(400, "AI配置不完整：缺少地址或模型");

            var endpoint = BuildChatEndpoint(cfg.BaseUrl);
            var messages = new JArray();
            if (!string.IsNullOrWhiteSpace(req.SystemPrompt))
            {
                messages.Add(new JObject
                {
                    ["role"] = "system",
                    ["content"] = req.SystemPrompt.Trim()
                });
            }
            messages.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = req.Message.Trim()
            });

            var model = NormalizeModelName(cfg.BaseUrl, cfg.Model);

            var payload = new JObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["stream"] = false
            };
            if (req.Temperature != null)
                payload["temperature"] = req.Temperature.Value;

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(GetRequestTimeoutSeconds(cfg.TimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(cfg.ApiKey))
                request.Headers.Add("Authorization", $"Bearer {cfg.ApiKey}");

            HttpResponseMessage response;
            string body;
            try
            {
                response = await client.SendAsync(request);
                body = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                return BuildResult(500, $"调用AI接口失败：{ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var parsedMessage = TryGetUpstreamErrorMessage(body);
                return BuildResult(500, $"AI接口返回异常：{(int)response.StatusCode}", new
                {
                    endpoint,
                    body,
                    upstreamMessage = parsedMessage
                });
            }

            try
            {
                var json = JObject.Parse(body);
                var reply = json["choices"]?[0]?["message"]?["content"]?.ToString();
                if (string.IsNullOrWhiteSpace(reply))
                    reply = json["choices"]?[0]?["text"]?.ToString();

                return BuildResult(0, "success", new
                {
                    reply = reply ?? string.Empty,
                    model = json["model"]?.ToString() ?? model,
                    usage = json["usage"]
                });
            }
            catch (Exception ex)
            {
                return BuildResult(500, $"解析AI响应失败：{ex.Message}", new { body });
            }
        }

        public async Task<IActionResult> OnPostSendStream([FromBody] ChatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Message))
                return BuildResult(400, "消息不能为空");

            var cfg = req.ConfigId > 0 ? AIConfig.Get(req.ConfigId) : AIConfig.GetDefault();
            if (cfg == null)
                return BuildResult(400, "请先配置可用的AI模型");
            if (!cfg.InUsed)
                return BuildResult(400, "当前配置已禁用");
            if (string.IsNullOrWhiteSpace(cfg.BaseUrl) || string.IsNullOrWhiteSpace(cfg.Model))
                return BuildResult(400, "AI配置不完整：缺少地址或模型");

            var endpoint = BuildChatEndpoint(cfg.BaseUrl);
            var messages = new JArray();
            if (!string.IsNullOrWhiteSpace(req.SystemPrompt))
            {
                messages.Add(new JObject
                {
                    ["role"] = "system",
                    ["content"] = req.SystemPrompt.Trim()
                });
            }
            messages.Add(new JObject
            {
                ["role"] = "user",
                ["content"] = req.Message.Trim()
            });

            var model = NormalizeModelName(cfg.BaseUrl, cfg.Model);
            var payload = new JObject
            {
                ["model"] = model,
                ["messages"] = messages,
                ["stream"] = true
            };
            if (req.Temperature != null)
                payload["temperature"] = req.Temperature.Value;

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(GetRequestTimeoutSeconds(cfg.TimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(cfg.ApiKey))
                request.Headers.Add("Authorization", $"Bearer {cfg.ApiKey}");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception ex)
            {
                return BuildResult(500, $"调用AI接口失败：{ex.Message}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                var parsedMessage = TryGetUpstreamErrorMessage(err);
                return BuildResult(500, $"AI接口返回异常：{(int)response.StatusCode}", new
                {
                    endpoint,
                    body = err,
                    upstreamMessage = parsedMessage
                });
            }

            Response.StatusCode = 200;
            Response.ContentType = "text/plain; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            await using var upstream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(upstream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;

                var data = line.Substring(5).Trim();
                if (string.IsNullOrWhiteSpace(data) || data == "[DONE]")
                    continue;

                try
                {
                    var json = JObject.Parse(data);
                    var delta = json["choices"]?[0]?["delta"]?["content"]?.ToString();
                    if (string.IsNullOrEmpty(delta))
                        delta = json["choices"]?[0]?["delta"]?["reasoning_content"]?.ToString();

                    if (!string.IsNullOrEmpty(delta))
                    {
                        await Response.WriteAsync(delta);
                        await Response.Body.FlushAsync();
                    }
                }
                catch
                {
                    // Ignore malformed stream chunks from upstream.
                }
            }

            return new EmptyResult();
        }

        private static string BuildChatEndpoint(string baseUrl)
        {
            var url = (baseUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                return url;

            if (url.EndsWith("/", StringComparison.Ordinal))
                url = url.TrimEnd('/');

            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return url + "/chat/completions";

            return url + "/chat/completions";
        }

        private static string NormalizeModelName(string baseUrl, string model)
        {
            var m = (model ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(m))
                return m;

            var isDeepseek = (baseUrl ?? string.Empty).IndexOf("deepseek", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isDeepseek)
                return m;

            if (string.Equals(m, "DeepSeekV3", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m, "deepseek-v3", StringComparison.OrdinalIgnoreCase))
                return "deepseek-chat";

            if (string.Equals(m, "DeepSeekR1", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m, "deepseek-r1", StringComparison.OrdinalIgnoreCase))
                return "deepseek-reasoner";

            return m;
        }

        private static string TryGetUpstreamErrorMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            try
            {
                var json = JObject.Parse(body);
                return json["error"]?["message"]?.ToString()
                       ?? json["message"]?.ToString()
                       ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int GetRequestTimeoutSeconds(int timeoutSeconds)
        {
            if (timeoutSeconds <= 0)
                return 300;
            return Math.Max(30, timeoutSeconds);
        }

        public class AIConfigOption
        {
            public long Id { get; set; }
            public string Name { get; set; }
            public string Model { get; set; }
        }

        public class ChatRequest
        {
            public long ConfigId { get; set; }
            public string Message { get; set; }
            public string SystemPrompt { get; set; }
            public double? Temperature { get; set; }
        }
    }
}
