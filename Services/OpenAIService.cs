using CivilGPT.Models;
using CivilGPT.Models.OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CivilGPT.Services
{
    public class OpenAIService
    {
        private readonly ChatSession _chatSession;
        private readonly HttpClient _httpClient;
        private readonly ChatRequest _chatRequest;

        public OpenAIService(ChatSession chatSession, ChatRequest chatRequest)
        {
            _chatSession = chatSession;
            _chatRequest = chatRequest;
            _httpClient = new HttpClient();
        }

        public async Task SendMessage(string prompt)
        {
            await Task.Delay(0);
            _chatSession.AddMessage(new ChatMessage(MessageRole.User, prompt));

            prompt = prompt.Trim().ToLower();

            switch (prompt)
            {
                case "привет":
                    _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "Привет! Как я могу помочь вам сегодня?"));
                    return;
                case "как дела?":
                    _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "У меня всё хорошо, спасибо! А у вас?"));
                    return;
                case "что ты умеешь?":
                    _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "Я могу отвечать на вопросы, помогать с задачами и предоставлять информацию по различным темам."));
                    return;
                case "пока":
                    _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "До свидания! Надеюсь, мы ещё поговорим."));
                    return;
                case "спасибо":
                    _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "Пожалуйста! Рад был помочь."));
                    return;
                case "помоги мне с кодом":
                    _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, "Конечно! Опишите, с чем именно вам нужна помощь, и я постараюсь помочь."));
                    return;
                default:
                    _chatSession.AddMessage(new ChatMessage(MessageRole.Assistant, $"Вы написали:{Environment.NewLine}{Environment.NewLine}{prompt}"));
                    return;
            }
        }
    }
}
