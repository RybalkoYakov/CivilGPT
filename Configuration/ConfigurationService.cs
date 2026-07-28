using CivilGPT.Models.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CivilGPT.Configuration
{
    public class ConfigurationService
    {
        private readonly AppSettings _settings;

        public ConfigurationService()
        {
            string json = File.ReadAllText("appsettings.json");
            _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? throw new InvalidOperationException("Failed to deserialize appsettings.json");
        }

        public string GetOpenAIApiKey()
        {
            return _settings.OpenAI.ApiKey;
        }
    }
}
