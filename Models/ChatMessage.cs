using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CivilGPT.Models
{
    public class ChatMessage
    {
        public MessageRole Role { get; set; }

        public string Content { get; set; }

        public ChatMessage(MessageRole role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}
