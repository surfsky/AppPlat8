using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
            var model = NormalizeModelName(cfg.BaseUrl, cfg.Model);
            var messageBuild = BuildUserMessageContent(req, model);
            if (!messageBuild.Success)
                return BuildResult(400, messageBuild.ErrorMessage);

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
                ["content"] = messageBuild.Content
            });

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
            var model = NormalizeModelName(cfg.BaseUrl, cfg.Model);
            var messageBuild = BuildUserMessageContent(req, model);
            if (!messageBuild.Success)
                return BuildResult(400, messageBuild.ErrorMessage);

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
                ["content"] = messageBuild.Content
            });
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

        private static BuiltMessageContent BuildUserMessageContent(ChatRequest req, string model)
        {
            var message = (req.Message ?? string.Empty).Trim();
            var attachments = req.Attachments?.Where(t => t != null).ToList() ?? new List<ChatAttachment>();
            if (attachments.Count == 0)
                return BuiltMessageContent.Ok(message);

            var imageAttachments = attachments.Where(IsImageAttachment).ToList();
            if (imageAttachments.Count > 0 && IsLikelyTextOnlyModel(model))
            {
                return BuiltMessageContent.Fail($"当前模型 {model} 可能不支持图片输入，请切换到视觉模型（如包含 vision/vl 的模型）后重试。");
            }

            if (imageAttachments.Count > 0)
            {
                var blocks = new JArray
                {
                    new JObject
                    {
                        ["type"] = "text",
                        ["text"] = message
                    }
                };

                foreach (var item in imageAttachments)
                {
                    if (string.IsNullOrWhiteSpace(item.DataUrl))
                        continue;

                    blocks.Add(new JObject
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new JObject
                        {
                            ["url"] = item.DataUrl
                        }
                    });
                }

                foreach (var item in attachments.Where(t => !IsImageAttachment(t)))
                {
                    var docText = BuildDocumentText(item);
                    if (!string.IsNullOrWhiteSpace(docText))
                    {
                        blocks.Add(new JObject
                        {
                            ["type"] = "text",
                            ["text"] = docText
                        });
                    }
                }

                return BuiltMessageContent.Ok(blocks);
            }

            var builder = new StringBuilder();
            builder.AppendLine(message);
            foreach (var item in attachments)
            {
                var docText = BuildDocumentText(item);
                if (!string.IsNullOrWhiteSpace(docText))
                {
                    builder.AppendLine();
                    builder.AppendLine(docText);
                }
            }

            return BuiltMessageContent.Ok(builder.ToString().Trim());
        }

        private static bool IsImageAttachment(ChatAttachment item)
        {
            if (item == null)
                return false;
            return (item.ContentType ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(item.DataUrl)
                   && item.DataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLikelyTextOnlyModel(string model)
        {
            var m = (model ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(m))
                return false;

            if (m.Contains("vision") || m.Contains("vl") || m.Contains("omni") || m.Contains("gpt-4o"))
                return false;

            return m.Contains("deepseek-chat") || m.Contains("deepseek-reasoner");
        }

        private static string BuildDocumentText(ChatAttachment item)
        {
            if (item == null)
                return string.Empty;

            var name = (item.Name ?? "附件").Trim();
            var safeName = Regex.Replace(name, "[\\r\\n]", " ");
            if (!string.IsNullOrWhiteSpace(item.TextContent))
            {
                var text = item.TextContent.Trim();
                if (text.Length > 12000)
                    text = text.Substring(0, 12000) + "\n...(内容过长，已截断)";

                return $"【附件: {safeName}】\n{text}";
            }

            return $"【附件: {safeName}】该文件无法直接提取文本。请上传 TXT/MD/CSV/JSON 等文本文件，或先将文档内容复制为文本后再分析。";
        }

        private sealed class BuiltMessageContent
        {
            public bool Success { get; private set; }
            public JToken Content { get; private set; }
            public string ErrorMessage { get; private set; }

            public static BuiltMessageContent Ok(JToken content) => new BuiltMessageContent
            {
                Success = true,
                Content = content
            };

            public static BuiltMessageContent Ok(string content) => Ok(new JValue(content ?? string.Empty));

            public static BuiltMessageContent Fail(string error) => new BuiltMessageContent
            {
                Success = false,
                ErrorMessage = error ?? "附件处理失败"
            };
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
            public List<ChatAttachment> Attachments { get; set; }
        }

        public class ChatAttachment
        {
            public string Name { get; set; }
            public string ContentType { get; set; }
            public string DataUrl { get; set; }
            public string TextContent { get; set; }
        }
    }
}
