using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CivilGPT.Models.OpenAI
{
    internal class ChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<Message> Messages { get; set; } = new();
    }
}
