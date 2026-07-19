using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace CivilGPT.Models.OpenAI
{
    public class ChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<Message> Message { get; set; } = new();
    }
}
