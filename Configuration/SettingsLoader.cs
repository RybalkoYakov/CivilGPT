using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CivilGPT.Infrastructure;

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
            var settings = new PluginSettings();

            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string configPath = Path.Combine(assemblyDir ?? string.Empty, "appsettings.json");

            if (File.Exists(configPath))
            {
                try
                {
                    ApplyJsonFile(settings, configPath);
                }
                catch (System.Exception)
                {
                    // Повреждённый или отсутствующий файл не должен ломать загрузку плагина:
                    // ниже останутся значения по умолчанию и переменные окружения.
                }
            }

            ApplyEnvironment(settings);

            return settings;
        }

        private static void ApplyJsonFile(PluginSettings settings, string configPath)
        {
            string json = File.ReadAllText(configPath);
            IDictionary<string, object> root = JsonHelper.AsDictionary(JsonHelper.Deserialize(json));

            if (root == null) return;

            IDictionary<string, object> ai = GetSection(root, "Ai");
            if (ai != null)
            {
                settings.Ai.Provider = JsonHelper.GetString(ai, "Provider", settings.Ai.Provider);

                IDictionary<string, object> deepSeek = GetSection(ai, "DeepSeek");
                if (deepSeek != null)
                {
                    settings.Ai.DeepSeek.ApiKey =
                        JsonHelper.GetString(deepSeek, "ApiKey", settings.Ai.DeepSeek.ApiKey);

                    settings.Ai.DeepSeek.BaseUrl =
                        JsonHelper.GetString(deepSeek, "BaseUrl", settings.Ai.DeepSeek.BaseUrl);

                    settings.Ai.DeepSeek.Model =
                        JsonHelper.GetString(deepSeek, "Model", settings.Ai.DeepSeek.Model);

                    double temperature;
                    if (JsonHelper.TryGetDouble(deepSeek, "Temperature", out temperature))
                    {
                        settings.Ai.DeepSeek.Temperature = temperature;
                    }
                }
            }

            IDictionary<string, object> plugin = GetSection(root, "Plugin");
            if (plugin != null)
            {
                settings.Plugin.LogLevel =
                    JsonHelper.GetString(plugin, "LogLevel", settings.Plugin.LogLevel);
            }
        }

        private static void ApplyEnvironment(PluginSettings settings)
        {
            settings.Ai.DeepSeek.ApiKey = EnvironmentVariable(
                "CIVILGPT_Ai__DeepSeek__ApiKey",
                EnvironmentVariable("DEEPSEEK_API_KEY", settings.Ai.DeepSeek.ApiKey));

            settings.Ai.DeepSeek.BaseUrl = EnvironmentVariable(
                "CIVILGPT_Ai__DeepSeek__BaseUrl", settings.Ai.DeepSeek.BaseUrl);

            settings.Ai.DeepSeek.Model = EnvironmentVariable(
                "CIVILGPT_Ai__DeepSeek__Model", settings.Ai.DeepSeek.Model);

            string temperature = EnvironmentVariable("CIVILGPT_Ai__DeepSeek__Temperature", null);
            double parsedTemperature;
            if (temperature != null
                && double.TryParse(
                    temperature,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out parsedTemperature))
            {
                settings.Ai.DeepSeek.Temperature = parsedTemperature;
            }
        }

        private static IDictionary<string, object> GetSection(
            IDictionary<string, object> source,
            string key)
        {
            object value;
            if (source == null || key == null) return null;
            if (!source.TryGetValue(key, out value)) return null;

            return JsonHelper.AsDictionary(value);
        }

        private static string EnvironmentVariable(string name, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
