namespace CivilGPT.AI.Tools
{
    /// <summary>
    /// Универсальный результат выполнения инструмента.
    /// Возвращается в LLM и в UI.
    /// </summary>
    public class ToolResult
    {
        public bool Success { get; set; }

        /// <summary>Человекочитаемый текст.</summary>
        public string Message { get; set; }

        public static ToolResult Ok(string message) =>
            new ToolResult { Success = true, Message = message };

        public static ToolResult Fail(string message) =>
            new ToolResult { Success = false, Message = message };
    }
}
