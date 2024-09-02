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
        // variable used to save user input to CosmosDb Storage.
        private readonly IStorage _myStorage;
        private string TimeStamp { get; set; }
        public CancellationToken cancellationToken { get; private set; }
        private ChatClient _client;
        private readonly ITranslator _translator;
        private BotState _dialogueState;
        private BotState _userState;
        public List<ChatMessage> conversationMessages = new List<ChatMessage>();
        private string DeploymentModel = "gpt-4o-mini";
        public class MessageStorage
        {
            public string UserMessage { get; set; }  
            public string AssistantMessage { get; set; }
            public string UserName { get; set; }
            public string Model { get; set; }
        }

        public EchoBot(IConfiguration configuration,ConversationState conversationState, UserState userState, IStorage storage)
        {
            // var endpoint = new Uri(configuration["Endpoint"]);
             
            _client = new(model: DeploymentModel, configuration["OpenAIApiKey"]);
            
            // DeepL
            _translator = new Translator(configuration["DeepLApiKey"]);

            // dialogue 
            _dialogueState = conversationState;
            _userState = userState;

            if (storage is null) throw new ArgumentNullException();
            _myStorage = storage;

        }

          public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            // ターンが終了した時に状態の変更を保存
            await base.OnTurnAsync(turnContext, cancellationToken);
            
            var storage = turnContext.TurnState.Get<IStorage>(nameof(IStorage));
            var userState = turnContext.TurnState.Get<UserState>(typeof(UserState).FullName);

            await _dialogueState.SaveChangesAsync(turnContext, false, cancellationToken);
            await _userState.SaveChangesAsync(turnContext, false, cancellationToken);
            
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // ConversationStateとUserStateを取得
            var conversationStateAccessors = _dialogueState.CreateProperty<DialogueRecord> (nameof(DialogueRecord));
            var conversationData = await conversationStateAccessors.GetAsync(turnContext, () => new DialogueRecord());

            var userStateAccessors = _userState.CreateProperty<UserProfile>(nameof(UserProfile));
            var userProfile = await userStateAccessors.GetAsync(turnContext, () => new UserProfile());

            // ユーザーがなければ作成、ユーザー名はTeamsから取得
            if (string.IsNullOrEmpty(userProfile.Name))
            {
                string userNamefromContext = turnContext.Activity.From.Name;
                userProfile.Name = userNamefromContext;
            }

            string userMessage = turnContext.Activity.Text;

            List<genAIMessage> GenAIMessages = conversationData.Messages ?? new List<genAIMessage>();
        　　// これまで会話があれば予め保持しておく
            if (conversationData.Messages?.Count > 0)
            {
                GenAIMessages = conversationData.Messages;
            }

            foreach (genAIMessage GenAIMessage in GenAIMessages)
            {
                switch (GenAIMessage.Role)
                {
                    case "user":
                        conversationMessages.Add(new UserChatMessage($"{GenAIMessage.Content}"));
                        break;
                    case "assistant":
                        conversationMessages.Add(new AssistantChatMessage($"{GenAIMessage.Content}"));
                        break;
                    default:
                        break;

                }
            }


            try
            {   
                // Translate text into a target language, in this case, French:
                var translatedText = await _translator.TranslateTextAsync(
                    userMessage,
                    null,
                    "en-US"
                    );

                await turnContext.SendActivityAsync(MessageFactory.Text($"[Accepted]:{translatedText}"), cancellationToken);
                
                GenAIMessages.Add(new genAIMessage { Role = "user", Content = $"{translatedText}" });
                conversationMessages.Add(new UserChatMessage($"{translatedText}"));
                
                ChatCompletion chatCompletion = _client.CompleteChat(
                    conversationMessages,
                    new ChatCompletionOptions()
                    {
                        MaxTokens = 2048,
                    }
                );



                await turnContext.SendActivityAsync(MessageFactory.Text($"{chatCompletion}"), cancellationToken);
                
                conversationMessages.Add(new AssistantChatMessage ($"{chatCompletion}"));
                GenAIMessages.Add(new genAIMessage { Role = "assistant", Content = $"{chatCompletion}" });
                
                // Make empty local log-items list.
                MessageStorage logItems = new MessageStorage();
                logItems.UserMessage = $"{translatedText}";
                logItems.AssistantMessage = $"{chatCompletion}" ;
                logItems.UserName = userProfile.Name;
                logItems.Model = DeploymentModel;
                TimeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");      
                // Create Dictionary object to hold new list of messages.
                var changes = new Dictionary<string, object>();
                {
                    changes.Add($"Chat_history_{TimeStamp}.json", logItems);
                };

                try
                {
                    // Save new list to your Storage.
                    await _myStorage.WriteAsync(changes,cancellationToken);
                }
                catch
                {
                    // Inform the user an error occurred.
                    await turnContext.SendActivityAsync("Sorry, something went wrong storing your message!");
                }

            }
            catch (Exception)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Sorry, something went wrong."), cancellationToken);
            }

            // 会話のステート保持
            var messageTimeOffSet = (System.DateTimeOffset)turnContext.Activity.Timestamp;
            var localMessageTime = messageTimeOffSet.ToLocalTime();
            conversationData.Timestamp = localMessageTime.ToString();
            conversationData.ChannelId = turnContext.Activity.ChannelId.ToString();
            conversationData.Messages = GenAIMessages;
         
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "I am an AI assistant.";
            conversationMessages.Add(new SystemChatMessage(welcomeText));
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