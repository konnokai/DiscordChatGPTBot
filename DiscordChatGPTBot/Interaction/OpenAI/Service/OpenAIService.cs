using OpenAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordChatGPTBot.Interaction.OpenAI.Service
{
    public class OpenAIService : IInteractionService
    {
        private readonly OpenAIClient _openAIClient;

        public OpenAIService(BotConfig botConfig)
        {
            _openAIClient = new OpenAIClient(new OpenAIAuthentication(botConfig.OpenAIToken));
        }
    }
}
