using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CivilGPT.AI.Tools;
using CivilGPT.CAD.Common;

namespace CivilGPT.AI
{
    /// <summary>
    /// Оркестратор «запрос → инструмент → ответ».
    /// 1. Отправляет историю и список инструментов в LLM.
    /// 2. Если LLM просит инструмент — выполняет его в главном потоке AutoCAD.
    /// 3. Возвращает результат обратно в LLM, чтобы получить человекочитаемый ответ.
    /// </summary>
    public class AiOrchestrator
    {
        private readonly ILlmClient _llm;
        private readonly ToolRegistry _tools;
        private readonly List<LlmMessage> _history = new List<LlmMessage>();

        public AiOrchestrator(ILlmClient llm, ToolRegistry tools)
        {
            _llm = llm ?? throw new ArgumentNullException(nameof(llm));
            _tools = tools ?? throw new ArgumentNullException(nameof(tools));

            _history.Add(LlmMessage.System(
                "Ты — помощник инженера-геодезиста и проектировщика в Autodesk Civil 3D. " +
                "Для изменения чертежа используй доступные инструменты. " +
                "Все линейные размеры — в метрах. Отвечай кратко и по-русски."));
        }

        public IReadOnlyList<LlmMessage> History => _history;

        public int ToolCount => _tools.All.Count;

        public void ClearHistory()
        {
            _history.RemoveAll(m => m.Role != "system");
        }

        public async Task<string> SendAsync(string userMessage)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return "Пустой запрос.";
            }

            _history.Add(LlmMessage.User(userMessage));

            List<LlmToolDefinition> toolDefinitions = _tools.All
                .Select(t => new LlmToolDefinition
                {
                    Name = t.Name,
                    Description = t.Description,
                    ParametersSchema = t.ParametersSchema
                })
                .ToList();

            LlmResponse response;
            try
            {
                // ConfigureAwait(false) обязателен: продолжение должно выполняться
                // не на потоке UI AutoCAD, иначе CadDispatcher ниже приведёт к взаимной блокировке.
                response = await _llm.SendAsync(_history, toolDefinitions).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return "Ошибка обращения к ИИ: " + ex.Message;
            }

            if (response == null || !response.HasToolCall)
            {
                string text = response?.Text ?? "(пустой ответ)";
                _history.Add(LlmMessage.Assistant(text));
                return text;
            }

            IPluginTool tool = _tools.Find(response.ToolName);
            if (tool == null)
            {
                string notFound = $"Инструмент '{response.ToolName}' не найден.";
                _history.Add(LlmMessage.Assistant(notFound));
                return notFound;
            }

            ToolResult result;
            try
            {
                JsonElement args = response.ToolArgs.ValueKind == JsonValueKind.Undefined
                    ? default(JsonElement)
                    : response.ToolArgs;

                result = CadDispatcher.Execute(() => tool.Execute(args));
            }
            catch (Exception ex)
            {
                result = ToolResult.Fail("Ошибка выполнения инструмента: " + ex.Message);
            }

            _history.Add(LlmMessage.Assistant($"Вызван инструмент {tool.Name}: {result.Message}"));

            // Второй проход: просим LLM сформулировать итог для пользователя.
            // Инструменты на этом проходе не передаются — исключаем бесконечный цикл вызовов.
            try
            {
                _history.Add(LlmMessage.User(
                    $"Результат выполнения инструмента '{tool.Name}': {result.Message} " +
                    "Сформулируй короткий итог для пользователя."));

                LlmResponse summary = await _llm.SendAsync(_history, new List<LlmToolDefinition>())
                    .ConfigureAwait(false);

                string text = string.IsNullOrWhiteSpace(summary?.Text) ? result.Message : summary.Text;
                _history.Add(LlmMessage.Assistant(text));
                return text;
            }
            catch (Exception)
            {
                // ИИ недоступен — возвращаем машинный результат, операция уже выполнена.
                _history.Add(LlmMessage.Assistant(result.Message));
                return result.Message;
            }
        }
    }
}
