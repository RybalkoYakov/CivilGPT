using System.Text.Json;

namespace CivilGPT.AI.Tools
{
    /// <summary>
    /// Контракт «инструмента» — атомарной операции, которую может вызвать ИИ или UI.
    /// Реализация обязана иметь открытый конструктор без параметров.
    /// </summary>
    public interface IPluginTool
    {
        /// <summary>Уникальное машинное имя (латиница, snake_case), например "randomize_point_elevation".</summary>
        string Name { get; }

        /// <summary>Описание для LLM: что делает инструмент и когда его вызывать.</summary>
        string Description { get; }

        /// <summary>JSON-схема аргументов (для function calling).</summary>
        JsonElement ParametersSchema { get; }

        /// <summary>Опасная операция (изменяет чертёж) — требует подтверждения пользователя.</summary>
        bool RequiresConfirmation { get; }

        /// <summary>Выполнить инструмент. Вызывается в главном потоке AutoCAD.</summary>
        ToolResult Execute(JsonElement args);
    }
}
