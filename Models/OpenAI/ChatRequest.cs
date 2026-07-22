using CivilGPT.Models.OpenAI;

namespace CivilGPT.Models.OpenAI
{
    public class ChatRequest
    {
        public string Model { get; set; } = "gpt-4.1-mini";

        public List<Message> Messages { get; set; } = new();

    }
}
