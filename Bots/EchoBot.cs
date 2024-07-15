using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using DeepL;

namespace EchoBot.Bots
{
    public class EchoBot : ActivityHandler
    {
        private ChatClient _client;
        private readonly ITranslator _translator;
        public EchoBot(IConfiguration configuration)
        {
            // var endpoint = new Uri(configuration["Endpoint"]);
            _client = new(model: "gpt-4o", configuration["OpenAIApiKey"]);
            
            _translator = new Translator(configuration["DeepLApiKey"]);

        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var userMessage = turnContext.Activity.Text;

            try
            {   

                // Translate text into a target language, in this case, French:
                var translatedText = await _translator.TranslateTextAsync(
                    userMessage,
                    null,
                    "en-US"
                    );

                await turnContext.SendActivityAsync(MessageFactory.Text($"[Accepted]:{translatedText}"), cancellationToken);

                ChatCompletion completion = await _client.CompleteChatAsync($"{translatedText}");

                await turnContext.SendActivityAsync(MessageFactory.Text($"{completion}"), cancellationToken);
            }
            catch (Exception)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, something went wrong."), cancellationToken);
            }

        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "I am an AI assistant.";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}