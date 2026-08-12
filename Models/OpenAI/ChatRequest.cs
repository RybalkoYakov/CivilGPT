using CivilGPT.Models.OpenAI;

namespace CivilGPT.Models.OpenAI
{
    public class ChatRequest
    {
        public string Model { get; set; } = "openai/gpt-4o";

        public List<Message> Messages { get; set; } = new();

    }
}
