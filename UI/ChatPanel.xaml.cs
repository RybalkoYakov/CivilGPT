using System;
using System.Windows.Controls;
using System.Windows.Input;
using CivilGPT.AI;

namespace CivilGPT.UI
{
    /// <summary>
    /// Панель чата с ИИ. Открывается командой CIVILGPT.
    /// </summary>
    public partial class ChatPanel : UserControl
    {
        private const string ReadyStatus = "Готов";

        private readonly AiOrchestrator _orchestrator;
        private bool _busy;

        public ChatPanel()
            : this(PluginExtension.Orchestrator)
        {
        }

        public ChatPanel(AiOrchestrator orchestrator)
        {
            InitializeComponent();

            _orchestrator = orchestrator;

            if (_orchestrator == null)
            {
                SetStatus("ИИ-слой не инициализирован");
            }
            else
            {
                SetStatus(ReadyStatus);
            }

            Append("CivilGPT: опишите задачу в поле ниже, например «раскидай точки COGO по высоте ±2 см» " +
                   "или «построй поверхность по горизонталям».");

            Append("Доступные команды AutoCAD: TINSURFACEFROMCONTOURS, RANDOMIZEPOINTZ, " +
                   "RANDOMIZEPOINTXY, CREATEDOFFSETPOINTS.");
        }

        private async void SendButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            await SendAsync();
        }

        private async void PromptBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            // Shift+Enter — перевод строки, Enter — отправка.
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) return;

            e.Handled = true;
            await SendAsync();
        }

        private async System.Threading.Tasks.Task SendAsync()
        {
            if (_busy || _orchestrator == null) return;

            string prompt = PromptBox.Text?.Trim();

            if (string.IsNullOrEmpty(prompt))
            {
                SetStatus("Введите запрос");
                return;
            }

            Append("Вы: " + prompt);
            PromptBox.Clear();

            _busy = true;
            SendButton.IsEnabled = false;
            SetStatus("ИИ обрабатывает запрос…");

            try
            {
                string answer = await _orchestrator.SendAsync(prompt);
                Append("CivilGPT: " + answer);
            }
            catch (Exception ex)
            {
                Append("Ошибка: " + ex.Message);
            }
            finally
            {
                _busy = false;
                SendButton.IsEnabled = true;
                SetStatus(ReadyStatus);
            }
        }

        private void ClearButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            HistoryBox.Clear();
            _orchestrator?.ClearHistory();
        }

        private void Append(string text)
        {
            HistoryBox.AppendText(text + Environment.NewLine + Environment.NewLine);
            HistoryBox.ScrollToEnd();
        }

        private void SetStatus(string text)
        {
            StatusText.Text = _orchestrator == null
                ? text
                : $"{text} · инструментов: {_orchestrator.ToolCount}";
        }
    }
}
