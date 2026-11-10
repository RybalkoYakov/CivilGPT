namespace CivilGPT.Configuration
{
    /// <summary>
    /// Корневой объект настроек плагина. Соответствует структуре appsettings.json.
    /// </summary>
    public class PluginSettings
    {
        public AiSettings Ai { get; set; } = new AiSettings();

        public PluginOptions Plugin { get; set; } = new PluginOptions();
    }

    public class AiSettings
    {
        public string Provider { get; set; } = "DeepSeek";

        public DeepSeekSettings DeepSeek { get; set; } = new DeepSeekSettings();
    }

    /// <summary>
    /// Настройки LLM-провайдера. Клиент совместим с OpenAI API,
    /// поэтому через BaseUrl можно подключить DeepSeek, OpenRouter или локальную Ollama.
    /// </summary>
    public class DeepSeekSettings
    {
        public string ApiKey { get; set; } = "";

        public string BaseUrl { get; set; } = "https://api.deepseek.com";

        public string Model { get; set; } = "deepseek-chat";

        public double Temperature { get; set; } = 0.2;
    }

    public class PluginOptions
    {
        public string LogLevel { get; set; } = "Info";
    }
}
