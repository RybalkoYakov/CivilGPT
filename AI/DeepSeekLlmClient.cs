using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CivilGPT.Configuration;
using CivilGPT.Infrastructure;

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
            var payload = new Dictionary<string, object>
            {
                ["model"] = _settings.Model,
                ["messages"] = BuildMessages(history),
                ["temperature"] = _settings.Temperature
            };

            if (tools != null && tools.Count > 0)
            {
                payload["tools"] = BuildTools(tools);
                payload["tool_choice"] = "auto";
            }

            string json = JsonHelper.Serialize(payload);
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

        private static List<object> BuildMessages(IReadOnlyList<LlmMessage> history)
        {
            var messages = new List<object>();
            if (history == null) return messages;

            foreach (LlmMessage message in history)
            {
                messages.Add(new Dictionary<string, object>
                {
                    ["role"] = message.Role,
                    ["content"] = message.Content ?? string.Empty
                });
            }

            return messages;
        }

        private static List<object> BuildTools(IReadOnlyList<LlmToolDefinition> tools)
        {
            var list = new List<object>();

            foreach (LlmToolDefinition tool in tools)
            {
                list.Add(new Dictionary<string, object>
                {
                    ["type"] = "function",
                    ["function"] = new Dictionary<string, object>
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = tool.ParametersSchema
                    }
                });
            }

            return list;
        }

        private static LlmResponse ParseResponse(string json)
        {
            IDictionary<string, object> root = JsonHelper.AsDictionary(JsonHelper.Deserialize(json));

            if (root == null)
            {
                throw new InvalidOperationException("Пустой ответ LLM.");
            }

            object choicesRaw;
            if (!root.TryGetValue("choices", out choicesRaw))
            {
                throw new InvalidOperationException("Ответ LLM не содержит поля 'choices'.");
            }

            IList<object> choices = JsonHelper.AsArray(choicesRaw);
            if (choices == null || choices.Count == 0)
            {
                throw new InvalidOperationException("Ответ LLM не содержит вариантов ответа.");
            }

            IDictionary<string, object> message =
                GetDictionary(JsonHelper.AsDictionary(choices[0]), "message");

            if (message == null)
            {
                throw new InvalidOperationException("Ответ LLM не содержит поля 'message'.");
            }

            object toolCallsRaw;
            if (message.TryGetValue("tool_calls", out toolCallsRaw))
            {
                IList<object> toolCalls = JsonHelper.AsArray(toolCallsRaw);

                if (toolCalls != null && toolCalls.Count > 0)
                {
                    IDictionary<string, object> function =
                        GetDictionary(JsonHelper.AsDictionary(toolCalls[0]), "function");

                    string name = JsonHelper.GetString(function, "name");
                    string argumentsJson = JsonHelper.GetString(function, "arguments", "{}");

                    IDictionary<string, object> arguments =
                        JsonHelper.AsDictionary(JsonHelper.Deserialize(argumentsJson));

                    return new LlmResponse
                    {
                        ToolName = name,
                        ToolArgs = arguments ?? new Dictionary<string, object>()
                    };
                }
            }

            return new LlmResponse
            {
                Text = JsonHelper.GetString(message, "content", string.Empty)
            };
        }

        private static IDictionary<string, object> GetDictionary(
            IDictionary<string, object> source,
            string key)
        {
            object value;
            if (source == null || key == null) return null;
            if (!source.TryGetValue(key, out value)) return null;

            return JsonHelper.AsDictionary(value);
        }

        private static string Truncate(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
            return value.Substring(0, max) + "…";
        }
    }
}
