using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CivilGPT.Models;
using CivilGPT.Models.OpenAI;
using CivilGPT.Services;

namespace CivilGPT
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ChatSession _chatSession = new ChatSession();
        private readonly ChatRequest _chatRequest = new ChatRequest();
        private readonly OpenAIService _openAIService;


        public MainWindow()
        {
            InitializeComponent();
            _openAIService = new OpenAIService(_chatSession, _chatRequest);
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            string prompt = PromptTextBox.Text;

            if (!IsValidPrompt(prompt))
            {
                ResponsesTextBox.Text = "Пожалуйста, введите текст для отправки.";
                return;
            }

            await _openAIService.SendMessage(prompt);

            PromptTextBox.Clear();
            ResponsesTextBox.Clear();

            foreach (ChatMessage message in _chatSession.Messages)
            {
                ResponsesTextBox.AppendText(
                    $"{message.Role}: {message.Content}{Environment.NewLine}{Environment.NewLine}");
            }
        }

        private bool IsValidPrompt(string input)
        {
            return !string.IsNullOrWhiteSpace(input);
        }
    }
}