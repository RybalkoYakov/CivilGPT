using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace CivilGPT.AI
{
    /// <summary>
    /// Абстракция над LLM-провайдером (DeepSeek, OpenRouter, OpenAI, Ollama).
    /// Реализация должна быть совместима с OpenAI Chat Completions API.
    /// </summary>
    public interface ILlmClient
    {
        /// <summary>
        /// Отправить диалог и доступные инструменты в LLM.
        /// Возвращает либо текстовый ответ, либо запрос на вызов инструмента.
        /// </summary>
        Task<LlmResponse> SendAsync(
            IReadOnlyList<LlmMessage> history,
            IReadOnlyList<LlmToolDefinition> tools);
    }

    public class LlmMessage
    {
        /// <summary>Роль: "system", "user", "assistant".</summary>
        public string Role { get; set; }

        public string Content { get; set; }

        public static LlmMessage User(string content) =>
            new LlmMessage { Role = "user", Content = content };

        public static LlmMessage Assistant(string content) =>
            new LlmMessage { Role = "assistant", Content = content };

        public static LlmMessage System(string content) =>
            new LlmMessage { Role = "system", Content = content };
    }

    public class LlmToolDefinition
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public JsonElement ParametersSchema { get; set; }
    }

    public class LlmResponse
    {
        /// <summary>Текстовый ответ, если LLM не вызывает инструмент.</summary>
        public string Text { get; set; }

        /// <summary>Имя инструмента, если LLM просит его вызвать.</summary>
        public string ToolName { get; set; }

        /// <summary>Аргументы вызова инструмента.</summary>
        public JsonElement ToolArgs { get; set; }

        public bool HasToolCall => !string.IsNullOrEmpty(ToolName);
    }
}
