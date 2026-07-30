using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using App.Components;
using App.DAL;
using App.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace App.Pages.AI
{
    [IgnoreAntiforgeryToken]
    [Auth(Power.AIChat)]
    public class ChatModel : AdminModel
    {
        public List<AIConfigOption> Configs { get; set; } = new();
        public long? DefaultConfigId { get; set; }
        public AIConfigOption DefaultConfig { get; set; }

        public void OnGet()
        {
            var list = AIConfig.Search(null, true).ToList();
            Configs = list.Select(t => new AIConfigOption
            {
                Id = t.Id,
                Name = t.Name,
                Model = t.Model,
                Remark = t.Remark,
                Logo = t.Logo
            }).ToList();
            var def = list.OrderByDescending(t => t.IsDefault).ThenBy(t => t.SortId).ThenBy(t => t.Id).FirstOrDefault();
            DefaultConfigId = def?.Id;
            DefaultConfig = def == null ? null : new AIConfigOption
            {
                Id = def.Id,
                Name = def.Name,
                Model = def.Model,
                Remark = def.Remark,
                Logo = def.Logo
            };
        }

        public async Task<IActionResult> OnPostSend([FromBody] ChatRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Message))
                return BuildResult(400, "消息不能为空");

            var cfg = req.ConfigId > 0 ? AIConfig.Get(req.ConfigId) : AIConfig.GetDefault();
            if (cfg == null)
                return BuildResult(400, "请先配置可用的AI模型");
            if (!cfg.IsEnabled)
                return BuildResult(400, "当前配置已禁用");
            if (string.IsNullOrWhiteSpace(cfg.BaseUrl) || string.IsNullOrWhiteSpace(cfg.Model))
                return BuildResult(400, "AI配置不完整：缺少地址或模型");

            var apiType = DetectApiType(cfg.BaseUrl);
            var endpoint = apiType == AiApiType.Responses
                ? BuildResponsesEndpoint(cfg.BaseUrl)
                : BuildChatCompletionsEndpoint(cfg.BaseUrl);
            var model = NormalizeModelName(cfg.BaseUrl, cfg.Model);

            var systemPrompt = !string.IsNullOrWhiteSpace(req.SystemPrompt) ? req.SystemPrompt.Trim() : null;
            List<object> messages;
            if (req.Messages != null && req.Messages.Count > 0)
            {
                messages = BuildMessagesFromHistory(req.Messages, model, systemPrompt);
            }
            else
            {
                var messageBuild = BuildUserMessageContent(req, model);
                if (!messageBuild.Success)
                    return BuildResult(400, messageBuild.ErrorMessage);
                messages = new List<object>();
                if (!string.IsNullOrWhiteSpace(systemPrompt))
                    messages.Add(new { role = "system", content = systemPrompt });
                messages.Add(new { role = "user", content = messageBuild.Content });
            }

            var payload = BuildRequestPayloadMulti(
                apiType,
                model,
                cfg.EnableWebSearch,
                messages,
                systemPrompt,
                req.Temperature,
                stream: false);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(GetRequestTimeoutSeconds(cfg.TimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
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
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                string reply = null;
                var citations = new List<object>();

                if (apiType == AiApiType.Responses)
                {
                    if (root.TryGetProperty("output", out var output)
                        && output.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in output.EnumerateArray())
                        {
                            if (!item.TryGetProperty("type", out var itemType)
                                || itemType.ValueKind != JsonValueKind.String
                                || itemType.ToString() != "message")
                                continue;
                            if (!item.TryGetProperty("content", out var contentArr)
                                || contentArr.ValueKind != JsonValueKind.Array)
                                continue;
                            foreach (var content in contentArr.EnumerateArray())
                            {
                                if (!content.TryGetProperty("type", out var ct)
                                    || ct.ValueKind != JsonValueKind.String
                                    || ct.ToString() != "output_text")
                                    continue;
                                if (content.TryGetProperty("text", out var text))
                                    reply = text.ToString();
                                if (content.TryGetProperty("annotations", out var annotations)
                                    && annotations.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var ann in annotations.EnumerateArray())
                                    {
                                        if (ann.TryGetProperty("type", out var at)
                                            && at.ToString() == "url_citation")
                                        {
                                            citations.Add(new
                                            {
                                                url = ann.TryGetProperty("url", out var urlNode) ? urlNode.ToString() : string.Empty,
                                                title = ann.TryGetProperty("title", out var titleNode) ? titleNode.ToString() : string.Empty
                                            });
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else if (root.TryGetProperty("choices", out var choices)
                    && choices.ValueKind == JsonValueKind.Array
                    && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message)
                        && message.ValueKind == JsonValueKind.Object
                        && message.TryGetProperty("content", out var content))
                    {
                        reply = content.ToString();
                    }

                    if (string.IsNullOrWhiteSpace(reply)
                        && firstChoice.TryGetProperty("text", out var text))
                    {
                        reply = text.ToString();
                    }
                }

                var modelName = root.TryGetProperty("model", out var modelNode)
                    ? modelNode.ToString()
                    : model;

                object usage = null;
                if (root.TryGetProperty("usage", out var usageNode))
                    usage = usageNode.Clone();

                return BuildResult(0, "success", new
                {
                    reply = reply ?? string.Empty,
                    model = modelName,
                    citations,
                    usage
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
            if (!cfg.IsEnabled)
                return BuildResult(400, "当前配置已禁用");
            if (string.IsNullOrWhiteSpace(cfg.BaseUrl) || string.IsNullOrWhiteSpace(cfg.Model))
                return BuildResult(400, "AI配置不完整：缺少地址或模型");

            var apiType = DetectApiType(cfg.BaseUrl);
            var endpoint = apiType == AiApiType.Responses
                ? BuildResponsesEndpoint(cfg.BaseUrl)
                : BuildChatCompletionsEndpoint(cfg.BaseUrl);
            var model = NormalizeModelName(cfg.BaseUrl, cfg.Model);

            // 构建 messages 数组：多轮对话历史（前端传入）> 单轮消息（兜底）
            var systemPrompt = !string.IsNullOrWhiteSpace(req.SystemPrompt) ? req.SystemPrompt.Trim() : null;
            List<object> messages;
            if (req.Messages != null && req.Messages.Count > 0)
            {
                messages = BuildMessagesFromHistory(req.Messages, model, systemPrompt);
            }
            else
            {
                var messageBuild = BuildUserMessageContent(req, model);
                if (!messageBuild.Success)
                    return BuildResult(400, messageBuild.ErrorMessage);
                messages = new List<object>();
                if (!string.IsNullOrWhiteSpace(systemPrompt))
                    messages.Add(new { role = "system", content = systemPrompt });
                messages.Add(new { role = "user", content = messageBuild.Content });
            }

            var instructionText = systemPrompt; // Responses API 用 instructions

            var payload = BuildRequestPayloadMulti(
                apiType,
                model,
                cfg.EnableWebSearch,
                messages,
                instructionText,
                req.Temperature,
                stream: true);

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(GetRequestTimeoutSeconds(cfg.TimeoutSeconds));

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
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
            var citations = new List<object>();
            string upstreamErrorMessage = null;
            string upstreamErrorCode = null;
            // 使用 StreamWriter 包装 Response.Body，配合 HttpCompletionOption.ResponseHeadersRead
            // 实现真正的流式推送（每条 SSE delta 立刻 flush）。
            var sw = new StreamWriter(Response.Body, new UTF8Encoding(false));
            await sw.FlushAsync();

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
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;

                    string delta = null;

                    if (apiType == AiApiType.Responses)
                    {
                        if (root.TryGetProperty("type", out var eventType)
                            && eventType.ValueKind == JsonValueKind.String)
                        {
                            var et = eventType.ToString();
                            if (et == "response.output_text.delta")
                            {
                                if (root.TryGetProperty("delta", out var d))
                                    delta = d.ToString();
                            }
                            else if (et == "response.output_text.annotation.added")
                            {
                                if (root.TryGetProperty("annotation", out var ann)
                                    && ann.ValueKind == JsonValueKind.Object
                                    && ann.TryGetProperty("type", out var at)
                                    && at.ToString() == "url_citation")
                                {
                                    citations.Add(new
                                    {
                                        url = ann.TryGetProperty("url", out var urlNode) ? urlNode.ToString() : string.Empty,
                                        title = ann.TryGetProperty("title", out var titleNode) ? titleNode.ToString() : string.Empty
                                    });
                                }
                            }
                            else if (et == "error" || et == "response.failed")
                            {
                                if (root.TryGetProperty("message", out var msg))
                                    upstreamErrorMessage = msg.ToString();
                                if (root.TryGetProperty("code", out var code))
                                    upstreamErrorCode = code.ToString();
                                if (root.TryGetProperty("response", out var resp)
                                    && resp.ValueKind == JsonValueKind.Object
                                    && resp.TryGetProperty("error", out var respErr)
                                    && respErr.ValueKind == JsonValueKind.Object)
                                {
                                    if (string.IsNullOrEmpty(upstreamErrorMessage)
                                        && respErr.TryGetProperty("message", out var rmsg))
                                        upstreamErrorMessage = rmsg.ToString();
                                    if (string.IsNullOrEmpty(upstreamErrorCode)
                                        && respErr.TryGetProperty("code", out var rcode))
                                        upstreamErrorCode = rcode.ToString();
                                }
                            }
                            // 其余事件（reasoning_*、web_search_call.*、response.created/in_progress/output_item.*）忽略
                        }
                    }
                    else if (root.TryGetProperty("choices", out var choices)
                        && choices.ValueKind == JsonValueKind.Array
                        && choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("delta", out var deltaNode)
                            && deltaNode.ValueKind == JsonValueKind.Object)
                        {
                            if (deltaNode.TryGetProperty("content", out var contentNode))
                                delta = contentNode.ToString();

                            if (string.IsNullOrEmpty(delta)
                                && deltaNode.TryGetProperty("reasoning_content", out var reasonNode))
                                delta = reasonNode.ToString();
                        }
                    }

                    if (!string.IsNullOrEmpty(delta))
                    {
                        await sw.WriteAsync(delta);
                        await sw.FlushAsync();
                    }
                }
                catch
                {
                    // Ignore malformed stream chunks from upstream.
                }
            }

            if (!string.IsNullOrEmpty(upstreamErrorMessage))
            {
                var errPayload = JsonSerializer.Serialize(new
                {
                    error = upstreamErrorMessage,
                    code = upstreamErrorCode
                });
                await sw.WriteAsync("\n\n__ERROR__:" + errPayload + "__END__");
                await sw.FlushAsync();
            }

            if (citations.Count > 0)
            {
                var citationsJson = JsonSerializer.Serialize(citations);
                await sw.WriteAsync("\n\n__CITATIONS__:" + citationsJson + "__END__");
                await sw.FlushAsync();
            }
            await sw.FlushAsync();

            return new EmptyResult();
        }

        private enum AiApiType { ChatCompletions, Responses }

        private static AiApiType DetectApiType(string baseUrl)
        {
            var url = (baseUrl ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
            if (url.EndsWith("/responses") || url.Contains("/api/v3/responses"))
                return AiApiType.Responses;
            return AiApiType.ChatCompletions;
        }

        private static string BuildChatCompletionsEndpoint(string baseUrl)
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

        private static string BuildResponsesEndpoint(string baseUrl)
        {
            var url = (baseUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            url = url.TrimEnd('/');
            if (url.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
                return url;
            return url + "/responses";
        }

        private static object BuildResponsesUserContent(object chatContent)
        {
            if (chatContent is string s)
            {
                return new[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "input_text",
                        ["text"] = s
                    }
                };
            }

            if (chatContent is List<object> blocks)
            {
                var translated = new List<object>();
                foreach (var block in blocks)
                {
                    if (block is Dictionary<string, object> dict)
                    {
                        var type = dict.TryGetValue("type", out var t) ? t?.ToString() : null;
                        if (type == "text")
                        {
                            var text = dict.TryGetValue("text", out var tx) ? tx?.ToString() ?? string.Empty : string.Empty;
                            translated.Add(new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = text
                            });
                            continue;
                        }
                        if (type == "image_url")
                        {
                            string url = null;
                            if (dict.TryGetValue("image_url", out var iu))
                            {
                                if (iu is Dictionary<string, object> iuDict && iuDict.TryGetValue("url", out var u))
                                    url = u?.ToString();
                                else if (iu is string us)
                                    url = us;
                            }
                            if (!string.IsNullOrEmpty(url))
                            {
                                translated.Add(new Dictionary<string, object>
                                {
                                    ["type"] = "input_image",
                                    ["image_url"] = url
                                });
                            }
                            continue;
                        }
                    }
                    translated.Add(block);
                }
                return translated;
            }

            return new[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "input_text",
                    ["text"] = chatContent?.ToString() ?? string.Empty
                }
            };
        }

        private static object BuildRequestPayloadMulti(
            AiApiType apiType,
            string model,
            bool enableWebSearch,
            List<object> messages,
            string systemPromptForResponses,
            double? temperature,
            bool stream)
        {
            if (apiType == AiApiType.Responses)
            {
                var input = new List<object>();
                foreach (var msg in messages)
                {
                    // msg is anonymous type { role, content }
                    var role = msg.GetType().GetProperty("role")?.GetValue(msg)?.ToString() ?? "user";
                    var content = msg.GetType().GetProperty("content")?.GetValue(msg);

                    if (role == "system")
                        continue; // Responses 用 instructions，不发 system 消息

                    input.Add(new Dictionary<string, object>
                    {
                        ["role"] = role,
                        ["content"] = role == "user" ? BuildResponsesUserContent(content) : content
                    });
                }

                var payload = new Dictionary<string, object>
                {
                    ["model"] = model,
                    ["input"] = input,
                    ["stream"] = stream
                };
                if (!string.IsNullOrWhiteSpace(systemPromptForResponses))
                    payload["instructions"] = systemPromptForResponses.Trim();
                if (temperature != null)
                    payload["temperature"] = temperature.Value;
                if (enableWebSearch)
                {
                    payload["tools"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["type"] = "web_search",
                            ["max_keyword"] = 3
                        }
                    };
                }
                return payload;
            }

            // Chat Completions
            var chatPayload = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = messages,
                ["stream"] = stream
            };
            if (temperature != null)
                chatPayload["temperature"] = temperature.Value;
            return chatPayload;
        }

        /// <summary>
        /// 从多轮对话历史构建 messages 列表
        /// </summary>
        private static List<object> BuildMessagesFromHistory(
            List<ChatHistoryMessage> history,
            string model,
            string systemPrompt)
        {
            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
                messages.Add(new { role = "system", content = systemPrompt });

            var textOnly = IsLikelyTextOnlyModel(model);

            foreach (var histMsg in history)
            {
                if (histMsg == null || string.IsNullOrWhiteSpace(histMsg.Role))
                    continue;

                var role = histMsg.Role.Trim().ToLowerInvariant();

                if (role == "assistant")
                {
                    var text = histMsg.Content ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text))
                        messages.Add(new { role = "assistant", content = text });
                    continue;
                }

                if (role == "user")
                {
                    var content = BuildHistoryMessageContent(histMsg, textOnly);
                    messages.Add(new { role = "user", content = content });
                    continue;
                }

                // system / other roles: pass through as-is
                if (!string.IsNullOrWhiteSpace(histMsg.Content))
                    messages.Add(new { role = role, content = histMsg.Content });
            }

            return messages;
        }

        /// <summary>
        /// 将历史消息中的一条 user 消息转换成 content（字符串 或 内容块数组）
        /// </summary>
        private static object BuildHistoryMessageContent(ChatHistoryMessage msg, bool textOnlyModel)
        {
            var text = msg.Content ?? string.Empty;
            var attachments = msg.Attachments;
            if (attachments == null || attachments.Count == 0)
                return text;

            // 纯文本模型：图片附件只能转文本占位，文件内容附在末尾
            if (textOnlyModel)
            {
                var sb = new StringBuilder(text);
                foreach (var att in attachments)
                {
                    if ((att.ContentType ?? string.Empty).StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                        sb.AppendLine("[用户曾发送图片: " + (att.Name ?? "图片") + "]");
                    else
                    {
                        var docText = BuildDocumentText(att);
                        if (!string.IsNullOrWhiteSpace(docText))
                        {
                            sb.AppendLine();
                            sb.AppendLine(docText);
                        }
                    }
                }
                return sb.ToString().Trim();
            }

            var imageAtts = attachments.Where(IsImageAttachment).ToList();
            var fileAtts = attachments.Where(t => !IsImageAttachment(t)).ToList();

            // 无图片附件：文本内容 + 文件内容
            if (imageAtts.Count == 0)
            {
                var sb = new StringBuilder(text);
                foreach (var att in fileAtts)
                {
                    var docText = BuildDocumentText(att);
                    if (!string.IsNullOrWhiteSpace(docText))
                    {
                        sb.AppendLine();
                        sb.AppendLine(docText);
                    }
                }
                return sb.ToString().Trim();
            }

            // 有图片附件：构建 content 块数组
            var blocks = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = text
                }
            };

            foreach (var att in imageAtts)
            {
                if (string.IsNullOrWhiteSpace(att.DataUrl))
                    continue;
                blocks.Add(new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new Dictionary<string, object>
                    {
                        ["url"] = att.DataUrl
                    }
                });
            }

            foreach (var att in fileAtts)
            {
                var docText = BuildDocumentText(att);
                if (!string.IsNullOrWhiteSpace(docText))
                {
                    blocks.Add(new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = docText
                    });
                }
            }

            return blocks;
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
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var errorNode)
                    && errorNode.ValueKind == JsonValueKind.Object
                    && errorNode.TryGetProperty("message", out var errorMessage))
                {
                    return errorMessage.ToString();
                }

                if (root.TryGetProperty("message", out var messageNode))
                    return messageNode.ToString();

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static int GetRequestTimeoutSeconds(int timeoutSeconds)
        {
            // 流式响应需要给 web_search、模型思考预留充足时间。
            // 默认上限 600 秒（10 分钟），下限 120 秒（避免 < 2 分钟导致联网时被截断）。
            if (timeoutSeconds <= 0)
                return 600;
            return Math.Max(120, Math.Min(timeoutSeconds, 600));
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
                var blocks = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "text",
                        ["text"] = message
                    }
                };

                foreach (var item in imageAttachments)
                {
                    if (string.IsNullOrWhiteSpace(item.DataUrl))
                        continue;

                    blocks.Add(new Dictionary<string, object>
                    {
                        ["type"] = "image_url",
                        ["image_url"] = new Dictionary<string, object>
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
                        blocks.Add(new Dictionary<string, object>
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
            public object Content { get; private set; }
            public string ErrorMessage { get; private set; }


            //public static BuiltMessageContent Ok(string content) => Ok(content ?? string.Empty);
            public static BuiltMessageContent Ok(object content) => new BuiltMessageContent
            {
                Success = true,
                Content = content
            };

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
            public string Remark { get; set; }
            public string Logo { get; set; }
        }

        public class ChatRequest
        {
            public long ConfigId { get; set; }
            public string Message { get; set; }
            public string SystemPrompt { get; set; }
            public double? Temperature { get; set; }
            public List<ChatAttachment> Attachments { get; set; }
            /// <summary>多轮对话历史（含当前消息），前端传入时后端不再自行构造单条 user</summary>
            public List<ChatHistoryMessage> Messages { get; set; }
        }

        public class ChatHistoryMessage
        {
            public string Role { get; set; }
            public string Content { get; set; }
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
