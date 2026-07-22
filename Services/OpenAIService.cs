using CivilGPT.Models;
using CivilGPT.Models.OpenAI;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CivilGPT.Services
{
    public class OpenAIService
    {
        private const string ChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";

        private readonly ChatSession _chatSession;
        private readonly ConfigurationService _configurationService;
        private readonly HttpClient _httpClient;

        public OpenAIService(ChatSession chatSession)
        {
            _chatSession = chatSession;
            _configurationService = new ConfigurationService();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                     _configurationService.GetOpenAIApiKey());
        }

        public async Task SendMessage(string prompt)
        {
            await Task.Delay(0);
            _chatSession.AddMessage(new ChatMessage(MessageRole.User, prompt));

            string json = BuildJSON();

            StringContent content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response = 
                await _httpClient.PostAsync(ChatCompletionsUrl, content);

            string responseJson = await response.Content.ReadAsStringAsync();

            _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, responseJson));

            //prompt = prompt.Trim().ToLower();

            //switch (prompt)
            //{
            //    case "привет":
            //        _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "Привет! Как я могу помочь вам сегодня?"));
            //        return;
            //    case "как дела?":
            //        _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "У меня всё хорошо, спасибо! А у вас?"));
            //        return;
            //    case "что ты умеешь?":
            //        _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "Я могу отвечать на вопросы, помогать с задачами и предоставлять информацию по различным темам."));
            //        return;
            //    case "пока":
            //        _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "До свидания! Надеюсь, мы ещё поговорим."));
            //        return;
            //    case "спасибо":
            //        _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "Пожалуйста! Рад был помочь."));
            //        return;
            //    case "помоги мне с кодом":
            //        _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "Конечно! Опишите, с чем именно вам нужна помощь, и я постараюсь помочь."));
            //        return;
            //    default:
            //        _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, $"Вы написали:{Environment.NewLine}{Environment.NewLine}{prompt}"));
            //        return;
            //}
        }

        private ChatRequest BuildRequest()
        {
            ChatRequest request = new ChatRequest();

            foreach (ChatMessage chatMessage in _chatSession.Messages)
            {
                request.Messages.Add(new Message
                {
                    Role = chatMessage.Role.ToString().ToLower(),
                    Content = chatMessage.Content
                });
            }

            return request;
        }

        private string BuildJSON()
        {
            ChatRequest chatRequest = BuildRequest();

            return JsonSerializer.Serialize(chatRequest, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
