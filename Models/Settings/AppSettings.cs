using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CivilGPT.Models.Settings
{
    public class AppSettings
    {
        public OpenAISettings OpenAI { get; set; } = new OpenAISettings();
    }
}
