using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CivilGPT.Configuration;

namespace CivilGPT.AI
{
    /// <summary>
    /// Клиент, совместимый с OpenAI Chat Completions API (включая function calling).
    /// По умолчанию настроен на DeepSeek; OpenRouter/OpenAI/Ollama подключаются через BaseUrl.
    /// </summary>
    public class DeepSeekLlmClient : ILlmClient
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        private readonly DeepSeekSettings _settings;

        public DeepSeekLlmClient(DeepSeekSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                throw new ArgumentException("Ключ API не задан.", nameof(settings));
            }
        }

        public async Task<LlmResponse> SendAsync(
            IReadOnlyList<LlmMessage> history,
            IReadOnlyList<LlmToolDefinition> tools)
        {
            var payload = new
            {
                model = _settings.Model,
                messages = BuildMessages(history),
                tools = BuildTools(tools),
                tool_choice = tools != null && tools.Count > 0 ? "auto" : null,
                temperature = _settings.Temperature
            };

            string json = JsonSerializer.Serialize(payload);
            string url = _settings.BaseUrl.TrimEnd('/') + "/chat/completions";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                request.Headers.Add("Authorization", "Bearer " + _settings.ApiKey);

                HttpResponseMessage response = await Http.SendAsync(request).ConfigureAwait(false);
                string responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"LLM API вернул {response.StatusCode}: {Truncate(responseJson, 800)}");
                }

                return ParseResponse(responseJson);
            }
        }

        private static object[] BuildMessages(IReadOnlyList<LlmMessage> history)
        {
            var messages = new List<object>();
            foreach (LlmMessage message in history)
            {
                messages.Add(new { role = message.Role, content = message.Content });
            }
            return messages.ToArray();
        }

        private static object[] BuildTools(IReadOnlyList<LlmToolDefinition> tools)
        {
            if (tools == null || tools.Count == 0) return null;

            var list = new List<object>();
            foreach (LlmToolDefinition tool in tools)
            {
                list.Add(new
                {
                    type = "function",
                    function = new
                    {
                        name = tool.Name,
                        description = tool.Description,
                        parameters = tool.ParametersSchema
                    }
                });
            }
            return list.ToArray();
        }

        private static LlmResponse ParseResponse(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;

                if (!root.TryGetProperty("choices", out JsonElement choices)
                    || choices.ValueKind != JsonValueKind.Array
                    || choices.GetArrayLength() == 0)
                {
                    throw new InvalidOperationException("Ответ LLM не содержит поля 'choices'.");
                }

                JsonElement message = choices[0].GetProperty("message");

                if (message.TryGetProperty("tool_calls", out JsonElement toolCalls)
                    && toolCalls.ValueKind == JsonValueKind.Array
                    && toolCalls.GetArrayLength() > 0)
                {
                    JsonElement function = toolCalls[0].GetProperty("function");

                    string argumentsJson = "{}";
                    if (function.TryGetProperty("arguments", out JsonElement arguments)
                        && arguments.ValueKind == JsonValueKind.String)
                    {
                        argumentsJson = arguments.GetString() ?? "{}";
                    }

                    JsonElement parsedArguments;
                    using (JsonDocument argumentDocument =
                        JsonDocument.Parse(string.IsNullOrWhiteSpace(argumentsJson) ? "{}" : argumentsJson))
                    {
                        parsedArguments = argumentDocument.RootElement.Clone();
                    }

                    return new LlmResponse
                    {
                        ToolName = function.GetProperty("name").GetString(),
                        ToolArgs = parsedArguments
                    };
                }

                string text = "";
                if (message.TryGetProperty("content", out JsonElement content)
                    && content.ValueKind == JsonValueKind.String)
                {
                    text = content.GetString() ?? "";
                }

                return new LlmResponse { Text = text };
            }
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max) + "…";
        }
    }
}
