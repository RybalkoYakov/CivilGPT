using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace CivilGPT.Configuration
{
    /// <summary>
    /// Загружает настройки из appsettings.json и переменных окружения.
    /// Приоритет: переменные окружения > appsettings.json > значения по умолчанию.
    /// Ключ API в репозитории не хранится.
    /// </summary>
    public static class SettingsLoader
    {
        public static PluginSettings Load()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string configPath = Path.Combine(assemblyDir ?? string.Empty, "appsettings.json");

            var builder = new ConfigurationBuilder();

            if (File.Exists(configPath))
            {
                builder.AddJsonFile(configPath, optional: false, reloadOnChange: false);
            }

            // Пример переопределения: CIVILGPT_Ai__DeepSeek__ApiKey=sk-xxx
            builder.AddEnvironmentVariables(prefix: "CIVILGPT_");

            IConfigurationRoot config = builder.Build();

            var settings = new PluginSettings();
            config.Bind(settings);

            if (string.IsNullOrWhiteSpace(settings.Ai.DeepSeek.ApiKey))
            {
                settings.Ai.DeepSeek.ApiKey =
                    Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY") ?? "";
            }

            return settings;
        }
    }
}
