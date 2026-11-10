using System;
using Autodesk.AutoCAD.Runtime;
using CivilGPT.AI;
using CivilGPT.CAD.Common;
using CivilGPT.Configuration;

[assembly: ExtensionApplication(typeof(CivilGPT.PluginExtension))]

namespace CivilGPT
{
    /// <summary>
    /// Точка входа плагина: вызывается AutoCAD при загрузке сборки через NETLOAD.
    /// </summary>
    public class PluginExtension : IExtensionApplication
    {
        public static PluginSettings Settings { get; private set; }

        public static AiOrchestrator Orchestrator { get; private set; }

        public static ToolRegistry Tools { get; private set; }

        public void Initialize()
        {
            // Исключение в Initialize может уронить AutoCAD,
            // поэтому весь код инициализации изолирован.
            try
            {
                InitializeCore();
            }
            catch (System.Exception ex)
            {
                CadApplication.WriteMessage(
                    "\n[CivilGPT] Ошибка инициализации: " + ex.Message);
            }
        }

        public void Terminate()
        {
            Orchestrator = null;
            Tools = null;
        }

        private static void InitializeCore()
        {
            Settings = SettingsLoader.Load();
            Tools = new ToolRegistry();

            bool hasApiKey = !string.IsNullOrWhiteSpace(Settings.Ai.DeepSeek.ApiKey);

            ILlmClient llm = hasApiKey
                ? (ILlmClient)new DeepSeekLlmClient(Settings.Ai.DeepSeek)
                : new StubLlmClient();

            Orchestrator = new AiOrchestrator(llm, Tools);

            CadApplication.WriteMessage(
                $"\n[CivilGPT] Плагин загружен. Инструментов в реестре: {Tools.All.Count}.");

            if (hasApiKey)
            {
                CadApplication.WriteMessage(
                    $"\n[CivilGPT] Модель: {Settings.Ai.DeepSeek.Model} ({Settings.Ai.DeepSeek.BaseUrl}).");
            }
            else
            {
                CadApplication.WriteMessage(
                    "\n[CivilGPT] Ключ API не задан — доступны только команды AutoCAD. " +
                    "Укажите CIVILGPT_Ai__DeepSeek__ApiKey или DEEPSEEK_API_KEY.");
            }

            CadApplication.WriteMessage(
                "\n[CivilGPT] Команды: CIVILGPT, TINSURFACEFROMCONTOURS, RANDOMIZEPOINTZ, " +
                "RANDOMIZEPOINTXY, CREATEDOFFSETPOINTS.");
        }
    }
}
