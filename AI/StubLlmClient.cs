using System.Collections.Generic;
using System.Threading.Tasks;

namespace CivilGPT.AI
{
    /// <summary>
    /// Заглушка LLM-клиента. Используется, когда ключ API не задан:
    /// плагин остаётся работоспособным, команды AutoCAD доступны напрямую.
    /// </summary>
    public class StubLlmClient : ILlmClient
    {
        public Task<LlmResponse> SendAsync(
            IReadOnlyList<LlmMessage> history,
            IReadOnlyList<LlmToolDefinition> tools)
        {
            return Task.FromResult(new LlmResponse
            {
                Text = "ИИ-слой не подключён: не задан ключ API. " +
                       "Укажите CIVILGPT_Ai__DeepSeek__ApiKey или переменную DEEPSEEK_API_KEY. " +
                       "Команды RANDOMIZEPOINTZ, RANDOMIZEPOINTXY, CREATEDOFFSETPOINTS, TINSURFACEFROMCONTOURS работают и без ИИ."
            });
        }
    }
}
