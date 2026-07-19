using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CivilGPT.Models
{
    public class ChatSession
    {
        private readonly List<ChatMessage> _messages = new();
        public IReadOnlyList<ChatMessage> Messages => _messages;
        public void AddMessage(ChatMessage message) { _messages.Add(message); }
    }
}
