using System;
using Autodesk.AutoCAD.Runtime;
using CivilGPT.AI;
using CivilGPT.CAD.Common;
using CivilGPT.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: ExtensionApplication(typeof(CivilGPT.PluginExtension))]

namespace CivilGPT
{
    /// <summary>
    /// РўРѕС‡РєР° РІС…РѕРґР° РїР»Р°РіРёРЅР°: РІС‹Р·С‹РІР°РµС‚СЃСЏ AutoCAD РїСЂРё Р·Р°РіСЂСѓР·РєРµ СЃР±РѕСЂРєРё С‡РµСЂРµР· NETLOAD.
    /// </summary>
    public class PluginExtension : IExtensionApplication
    {
        public static ServiceProvider Services { get; private set; }

        public static PluginSettings Settings { get; private set; }

        public static AiOrchestrator Orchestrator { get; private set; }

        public static ToolRegistry Tools { get; private set; }

        public void Initialize()
        {
            // РСЃРєР»СЋС‡РµРЅРёРµ РІ Initialize РјРѕР¶РµС‚ СѓСЂРѕРЅРёС‚СЊ AutoCAD,
            // РїРѕСЌС‚РѕРјСѓ РІРµСЃСЊ РєРѕРґ РёРЅРёС†РёР°Р»РёР·Р°С†РёРё РёР·РѕР»РёСЂРѕРІР°РЅ.
            try
            {
                InitializeCore();
            }
            catch (System.Exception ex)
            {
                CadApplication.WriteMessage(
                    "\n[CivilGPT] РћС€РёР±РєР° РёРЅРёС†РёР°Р»РёР·Р°С†РёРё: " + ex.Message);
            }
        }

        public void Terminate()
        {
            try
            {
                Services?.Dispose();
            }
            catch (System.Exception)
            {
                // РџРѕРґР°РІР»СЏРµРј РѕС€РёР±РєРё РѕСЃРІРѕР±РѕР¶РґРµРЅРёСЏ РїСЂРё РІС‹РіСЂСѓР·РєРµ РїР»Р°РіРёРЅР°.
            }

            Services = null;
            Orchestrator = null;
            Tools = null;
        }

        private static void InitializeCore()
        {
            Settings = SettingsLoader.Load();

            var services = new ServiceCollection();

            services.AddSingleton(Settings);
            services.AddSingleton(Settings.Ai.DeepSeek);
            services.AddSingleton<ToolRegistry>();
            services.AddSingleton<AiOrchestrator>();

            bool hasApiKey = !string.IsNullOrWhiteSpace(Settings.Ai.DeepSeek.ApiKey);

            if (hasApiKey)
            {
                services.AddSingleton<ILlmClient, DeepSeekLlmClient>();
            }
            else
            {
                services.AddSingleton<ILlmClient, StubLlmClient>();
            }

            Services = services.BuildServiceProvider();

            Orchestrator = Services.GetRequiredService<AiOrchestrator>();
            Tools = Services.GetRequiredService<ToolRegistry>();

            CadApplication.WriteMessage(
                $"\n[CivilGPT] РџР»Р°РіРёРЅ Р·Р°РіСЂСѓР¶РµРЅ. РРЅСЃС‚СЂСѓРјРµРЅС‚РѕРІ РІ СЂРµРµСЃС‚СЂРµ: {Tools.All.Count}.");

            if (hasApiKey)
            {
                CadApplication.WriteMessage(
                    $"\n[CivilGPT] РњРѕРґРµР»СЊ: {Settings.Ai.DeepSeek.Model} ({Settings.Ai.DeepSeek.BaseUrl}).");
            }
            else
            {
                CadApplication.WriteMessage(
                    "\n[CivilGPT] РљР»СЋС‡ API РЅРµ Р·Р°РґР°РЅ вЂ” РґРѕСЃС‚СѓРїРЅС‹ С‚РѕР»СЊРєРѕ РєРѕРјР°РЅРґС‹ AutoCAD. " +
                    "РЈРєР°Р¶РёС‚Рµ CIVILGPT_Ai__DeepSeek__ApiKey РёР»Рё DEEPSEEK_API_KEY.");
            }

            CadApplication.WriteMessage(
                "\n[CivilGPT] РљРѕРјР°РЅРґС‹: CIVILGPT, TINSURFACEFROMCONTOURS, RANDOMIZEPOINTZ, " +
                "RANDOMIZEPOINTXY, CREATEDOFFSETPOINTS.");
        }
    }
}

