using Autodesk.AutoCAD.Runtime;
using CivilGPT.UI;

namespace CivilGPT.Commands
{
    /// <summary>
    /// Команда открытия панели ИИ-помощника.
    /// </summary>
    public class ChatCommands
    {
        [CommandMethod("CIVILGPT")]
        public void ShowChatPalette()
        {
            if (PluginExtension.Orchestrator == null)
            {
                CAD.Common.CadApplication.WriteMessage(
                    "\n[CivilGPT] Инициализация плагина не завершена. Проверьте appsettings.json.");
                return;
            }

            ChatPalette.Show();
        }
    }
}
