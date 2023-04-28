using Discord;
using Discord.WebSocket;
using DiscordAdapter.Extensions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.TraceExtensions;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;

namespace DiscordAdapter
{
    public class DiscordAdapter : BotAdapter
    {
        /// <summary>
        /// Channel identifier for chat bots
        /// </summary>
        public static string ChannelId => "discord";
        private readonly DiscordAdapterOptions options;

        protected ILogger Logger { get; private init; }
        protected IDiscordBot DiscordBot { get; private init; }
        protected IServiceProvider ServiceProvider { get; private init; }
        protected MessageCachingService MessageCachingService { get; private init; }

        /// <summary>
        /// Active conversations by discord user id.
        /// </summary>
        protected ConcurrentDictionary<string, ConversationReference> ActiveConversations { get; private init; } = new();
        /// <summary>
        /// Last typing event time for each user who has typed before.
        /// </summary>
        protected ConcurrentDictionary<ulong, DateTimeOffset> UserLastTypeTimes { get; private init; } = new();

        public DiscordAdapter(
            IDiscordBot discordBot,
            ILogger<DiscordAdapter> logger,
            IServiceProvider serviceProvider,
            DiscordAdapterOptions options)
        {
            DiscordBot = discordBot;
            Logger = logger;
            ServiceProvider = serviceProvider;
            MessageCachingService = new MessageCachingService(
                discordBot.Client,
                serviceProvider.GetRequiredService<ILogger<MessageCachingService>>());

            OnTurnError = TurnErrorHandler;

            DiscordBot.Client.ButtonExecuted += HandleButtonClicked;
            DiscordBot.Client.MessageReceived += HandleMessageReceived;
            DiscordBot.Client.ReactionAdded += HandleReactionAdded;
            DiscordBot.Client.ReactionRemoved += HandleReactionRemoved;
            DiscordBot.Client.MessageDeleted += HandleMessageDeleted;
            DiscordBot.Client.MessageUpdated += HandleMessageUpdated;
            DiscordBot.Client.UserIsTyping += HandleUserIsTyping;
            this.options = options;
        }

        /// <summary>
        /// Handles Errors during bot code execution
        /// </summary>
        /// <param name="turnContext">Turn context during the bot's turn</param>
        /// <param name="exception">Exception thrown during the turn</param>
        /// <returns></returns>
        private async Task TurnErrorHandler(ITurnContext turnContext, Exception exception)
        {
            // Log any leaked exception from the application.
            Logger.LogError(exception, "[OnTurnError] unhandled error : {exceptionMessage}", exception.Message);

            // Send a message to the user
            var errorMessageText = "The bot encountered an error or bug.";
            var errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput);
            await turnContext.SendActivityAsync(errorMessage);

            errorMessageText = "To continue to run this bot, please fix the bot source code.";
            errorMessage = MessageFactory.Text(errorMessageText, errorMessageText, InputHints.ExpectingInput);
            await turnContext.SendActivityAsync(errorMessage);

            // Send a trace activity, which will be displayed in the Bot Framework Emulator
            await turnContext.TraceActivityAsync("OnTurnError Trace", exception.Message, "https://www.botframework.com/schemas/error", "TurnError");
        }

        /// <summary>
        /// Handles user typing events
        /// </summary>
        /// <param name="user">The user who started typing</param>
        /// <param name="channel">The chat where the typing has occurred</param>
        /// <returns></returns>
        protected virtual Task HandleUserIsTyping(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> channel)
        {
            _ = Task.Run(async () =>
            {
                if (!await DiscordBot.CheckUserIsTypingPrecondtions(user, channel))
                    return;

                IChannel c = channel.HasValue ? channel.Value : await DiscordBot.Client.GetDMChannelAsync(channel.Id);
                if (c != null && c is IDMChannel dm)
                {
                    IUser u = user.HasValue ? user.Value : await DiscordBot.Client.GetUserAsync(user.Id);
                    if (u != null && u.Id != DiscordBot.Client.CurrentUser.Id &&
                        ActiveConversations.TryGetValue(dm.Recipient.Id.ToString(), out var conversation))
                    {
                        var hasTyped = UserLastTypeTimes.TryGetValue(u.Id, out var typingTime);

                        var now = DateTimeOffset.UtcNow;

                        if (!hasTyped || 
                            (hasTyped && now.Subtract(typingTime).TotalSeconds >= 3.0))
                        {
                            var activity = new Activity
                            {
                                From = GetChannelAccount(u),
                                Recipient = GetChannelAccount(DiscordBot.Client.CurrentUser),
                                Type = ActivityTypes.Typing,
                                Conversation = conversation.Conversation
                            };

                            var bot = options.ChatBotFactory(ServiceProvider);
                            await ProcessActivityAsync(new ClaimsIdentity(), activity, bot.OnTurnAsync, default);

                            UserLastTypeTimes.AddOrUpdate(u.Id, now, (id, t) => now);
                        }
                    }
                }
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles messages being edited by users
        /// </summary>
        /// <param name="before">The message before the edit</param>
        /// <param name="after">The message after the edit</param>
        /// <param name="channel">The chat where the message was edited</param>
        /// <returns></returns>
        protected virtual Task HandleMessageUpdated(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel)
        {
            _ = Task.Run(async () =>
            {
                if (!await DiscordBot.CheckMessageUpdatedPrecondtions(before, after, channel))
                    return;

                if (channel != null && channel is IDMChannel dm)
                {
                    var oldActivity = MessageCachingService.GetRecievedMessageActivity(before.Id.ToString());
                    if (oldActivity != null && after.Author.Id != DiscordBot.Client.CurrentUser.Id &&
                        ActiveConversations.TryGetValue(dm.Recipient.Id.ToString(), out var conversation))
                    {
                        oldActivity.Text = after.Content;
                        oldActivity.Type = ActivityTypes.MessageUpdate;

                        var bot = options.ChatBotFactory(ServiceProvider);
                        await ProcessActivityAsync(new ClaimsIdentity(), (Activity)oldActivity, bot.OnTurnAsync, default);
                    }
                }
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles message deletes by users
        /// </summary>
        /// <param name="message">The message which was deleted</param>
        /// <param name="channel">the chat where the message was</param>
        /// <returns></returns>
        protected virtual Task HandleMessageDeleted(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            _ = Task.Run(async () =>
            {
                if (!(await DiscordBot.CheckMessageDeletedPrecondtions(message, channel)))
                    return;

                IChannel c = channel.HasValue ? channel.Value : await DiscordBot.Client.GetDMChannelAsync(channel.Id);
                if (c != null && c is IDMChannel dm)
                {
                    IUserMessage? m = MessageCachingService.GetUserMessage(message.Id);
                    if (m != null && m.Author.Id != DiscordBot.Client.CurrentUser.Id &&
                        ActiveConversations.TryGetValue(dm.Recipient.Id.ToString(), out var conversation))
                    {
                        var activity = new Activity
                        {
                            From = GetChannelAccount(m.Author),
                            Recipient = GetChannelAccount(DiscordBot.Client.CurrentUser),
                            Type = ActivityTypes.MessageDelete,
                            Conversation = conversation.Conversation,
                            Id = m.Id.ToString(),
                            Text = m.Content
                        };

                        var bot = options.ChatBotFactory(ServiceProvider);
                        await ProcessActivityAsync(new ClaimsIdentity(), activity, bot.OnTurnAsync, default);
                    }
                }
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles reactions being removed from messages by the users
        /// </summary>
        /// <param name="message">The message the reaction was removed from</param>
        /// <param name="channel">The chat where the message was</param>
        /// <param name="reaction">The reaction that was removed</param>
        /// <returns></returns>
        protected virtual Task HandleReactionRemoved(
            Cacheable<IUserMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel,
            SocketReaction reaction)
        {
            _ = Task.Run(async () =>
            {
                if (!(await DiscordBot.CheckReactionRemovedPrecondtions(message, channel, reaction)))
                    return;

                if (!(reaction.UserId != DiscordBot.Client.CurrentUser.Id &&
                    ActiveConversations.TryGetValue(reaction.UserId.ToString(), out var conversation)))
                {
                    return;
                }

                var user = await DiscordBot.Client.GetUserAsync(reaction.UserId);
                var activity = new Activity().CreateMessageReactionActivity(
                    message.Id.ToString(),
                    conversation.Conversation,
                    GetChannelAccount(user));

                activity.SetRemovedReaction(reaction.Emote);

                var bot = options.ChatBotFactory(ServiceProvider);
                await ProcessActivityAsync(new ClaimsIdentity(), activity, bot.OnTurnAsync, default);
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles reactions being added to messages by users
        /// </summary>
        /// <param name="message">The message the reaction was added to</param>
        /// <param name="channel">The chat where the message was</param>
        /// <param name="reaction">The reaction added</param>
        /// <returns></returns>
        protected virtual Task HandleReactionAdded(
            Cacheable<IUserMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel,
            SocketReaction reaction)
        {
            _ = Task.Run(async () =>
            {
                if (!(await DiscordBot.CheckReactionAddedPreconditions(message, channel, reaction)))
                    return;

                if (!(reaction.UserId != DiscordBot.Client.CurrentUser.Id &&
                    ActiveConversations.TryGetValue(reaction.UserId.ToString(), out var conversation)))
                {
                    return;
                }

                var user = await DiscordBot.Client.GetUserAsync(reaction.UserId);
                var activity = new Activity().CreateMessageReactionActivity(
                    message.Id.ToString(),
                    conversation.Conversation,
                    GetChannelAccount(user));

                activity.SetAddedReaction(reaction.Emote);

                var bot = options.ChatBotFactory(ServiceProvider);
                await ProcessActivityAsync(new ClaimsIdentity(), activity, bot.OnTurnAsync, default);
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles incoming user messages in DMs
        /// </summary>
        /// <param name="message">Message by a user</param>
        /// <returns></returns>
        protected virtual Task HandleMessageReceived(SocketMessage message)
        {
            // Long running event handlers will block the gateway thread, disrupting the websocket connection.
            // Creating and starting a new Task will prevent any blocking on the gateway thread
            _ = Task.Run(async () =>
            {
                if (!(await DiscordBot.CheckMessageReceivedPreconditions(message)))
                    return;

                if (!message.Author.IsBot && message.Channel is IDMChannel dm)
                {
                    var bot = options.ChatBotFactory(ServiceProvider);
                    await CreateOrContinueDiscordConversationAsync(dm, bot.OnTurnAsync, message);
                }
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles a button interaction event sent by discord. Used for handling the suggested action button clicks.
        /// </summary>
        /// <param name="button">Button that was clicked.</param>
        /// <returns></returns>
        protected virtual Task HandleButtonClicked(SocketMessageComponent button)
        {
            _ = Task.Run(async () =>
            {
                if (!(await DiscordBot.CheckButtonClickedPreconditions(button)))
                    return;

                if (button.Channel is IDMChannel dm)
                {
                    var message = GetMessageToBot(dm, button.Message);
                    message.Text = button.Data.CustomId;
                    var bot = options.ChatBotFactory(ServiceProvider);
                    await button.UpdateAsync(m =>
                    {
                        var cb = new ComponentBuilder();
                        foreach (var component in button.Message.Components.First().Components)
                        {
                            if (component is ButtonComponent btn)
                            {
                                cb.WithButton(
                                    btn.Label,
                                    btn.CustomId,
                                    style: btn.CustomId == button.Data.CustomId
                                        ? ButtonStyle.Primary : ButtonStyle.Secondary,
                                    disabled: true);
                            }
                        }
                        m.Components = cb.Build();
                    });
                    await ProcessActivityAsync(new ClaimsIdentity(), message, bot.OnTurnAsync, default);
                }
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// Turns suggested action cards into Discord buttons. Only 5 buttons will be created from the first 5 elements.
        /// </summary>
        /// <param name="suggestedActions">Suggested actions describing the options for selecting input.</param>
        /// <returns>Returns null if suggested action are null.</returns>
        private MessageComponent? CreateSuggestedActionButtons(SuggestedActions? suggestedActions)
        {
            if (suggestedActions is null || suggestedActions.Actions.Count == 0)
                return null;

            var cb = new ComponentBuilder();
            foreach (var card in suggestedActions.Actions.Take(5))
            {
                cb.WithButton(card.Title, card.Title);
            }
            return cb.Build();
        }

        public override async Task DeleteActivityAsync(
            ITurnContext turnContext,
            ConversationReference reference,
            CancellationToken cancellationToken)
        {
            if (!DiscordBot.IsConnected)
            {
                throw new ApplicationException("Discord bot is not connected!");
            }

            var user = await DiscordBot.Client.GetUserAsync(reference.Conversation.Id.ToUInt64());
            var dm = await user.CreateDMChannelAsync();
            var msg = await dm.GetMessageAsync(reference.ActivityId.ToUInt64());
            await msg.DeleteAsync();
        }

        public override async Task<ResourceResponse> UpdateActivityAsync(
            ITurnContext turnContext,
            Activity activity,
            CancellationToken cancellationToken)
        {
            if (!DiscordBot.IsConnected)
            {
                throw new ApplicationException("Discord bot is not connected!");
            }

            var response = new ResourceResponse { Id = activity.Id };
            var user = await DiscordBot.Client.GetUserAsync(activity.Conversation.Id.ToUInt64());
            var dm = await user.CreateDMChannelAsync();

            if (await dm.GetMessageAsync(activity.Id.ToUInt64()) is not IUserMessage msg)
            {
                return response;
            }

            await msg.ModifyAsync(m =>
            {
                m.Content = activity.Text;
            });
            return response;
        }

        public override async Task<ResourceResponse[]> SendActivitiesAsync(
            ITurnContext turnContext,
            Activity[] activities,
            CancellationToken cancellationToken)
        {
            if (!DiscordBot.IsConnected)
            {
                throw new ApplicationException("Discord bot is not connected!");
            }

            var resps = new ResourceResponse[activities.Length];
            for (int i = 0; i < activities.Length; i++)
            {
                string json = JsonConvert.SerializeObject(activities[i], Formatting.Indented, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                Logger.Log(LogLevel.Debug, "Outgoing activity: {json}", json);

                var activity = activities[i];
                IMessageActivity message;
                IMessageReactionActivity reaction;
                ITypingActivity typing;
                IEndOfConversationActivity endOfConversation;
                if ((message = activity.AsMessageActivity()) != null)
                {
                    ulong userId = ulong.Parse(message.Conversation.Id);
                    var attachments = await GetFilesFromAttachments(message.Attachments);
                    var sentMsg = await DiscordBot.SendMessageToUser(
                        userId,
                        message.Text ?? message.Speak,
                        components: CreateSuggestedActionButtons(message.SuggestedActions),
                        attachments: attachments);

                    ActiveConversations[userId.ToString()] = message.GetConversationReference();
                    message.Id = sentMsg.Id.ToString();
                    MessageCachingService.AddDiscordBotMessage(sentMsg);
                    MessageCachingService.AddSentMessageActivity(message);

                    attachments?.ForEach(a => a.Stream.Close());
                }
                else if ((reaction = activity.AsMessageReactionActivity()) != null)
                {
                    await AddAndRemoveReactionsFromMessageAsync(reaction);
                }
                else if ((typing = activity.AsTypingActivity()) != null)
                {
                    await TriggerTypingInDMAsync(typing);
                }
                else if((endOfConversation = activity.AsEndOfConversationActivity()) != null)
                {
                    await EndConversationAsync(endOfConversation);
                }

                resps[i] = new ResourceResponse
                {
                    Id = Guid.NewGuid().ToString(),
                };
            }

            return resps;
        }

        /// <summary>
        /// Ends a conversation with the given user described in the activity. Clears the message and activity cache for the user
        /// </summary>
        /// <param name="endOfConversation">Activity describing the ending if a conversation</param>
        /// <returns></returns>
        private async Task EndConversationAsync(IEndOfConversationActivity endOfConversation)
        {
            var userId = endOfConversation.Conversation.Id.ToUInt64();
            await DiscordBot.SendMessageToUser(userId, endOfConversation.Text ?? "The conversation has ended.");
            MessageCachingService.ClearCacheForUser(userId);
        }

        /// <summary>
        /// Triggers the typing indicator for the bot in chat described in the Activity
        /// </summary>
        /// <param name="typingActivity">Activity describing the chat where the typing indicator should be triggered</param>
        /// <returns></returns>
        private async Task TriggerTypingInDMAsync(ITypingActivity typingActivity)
        {
            var userId = typingActivity.Conversation.Id.ToUInt64();
            var user = await DiscordBot.Client.GetUserAsync(userId);
            var dm = await user.CreateDMChannelAsync();
            await dm.TriggerTypingAsync();
        }

        /// <summary>
        /// Adds and removes reactions by the bot to messages
        /// </summary>
        /// <param name="reactionActivity">Activity containing the reaction changes to a message</param>
        /// <returns></returns>
        private async Task AddAndRemoveReactionsFromMessageAsync(
            IMessageReactionActivity reactionActivity)
        {
            var userId = reactionActivity.Conversation.Id.ToUInt64();
            var messageId = reactionActivity.Id.ToUInt64();

            var user = await DiscordBot.Client.GetUserAsync(userId);
            var dm = await user.CreateDMChannelAsync();
            var message = await dm.GetMessageAsync(messageId);

            reactionActivity.ReactionsAdded?.ToList()
                .ForEach(async (reaction) => await message.AddReactionAsync(reaction.ToDiscordEmote()));

            var reacterId = reactionActivity.From?.Id.ToUInt64() ?? DiscordBot.Client.CurrentUser.Id;

            reactionActivity.ReactionsRemoved?.ToList()
                .ForEach(async (reaction) => await message.RemoveReactionAsync(reaction.ToDiscordEmote(), reacterId));
        }

        /// <summary>
        /// Turns Bot framework <see cref="Microsoft.Bot.Schema.Attachment"/> into Discord <see cref="FileAttachment"/> ready to be upload
        /// </summary>
        /// <param name="attachments"></param>
        /// <returns></returns>
        private async Task<List<FileAttachment>?> GetFilesFromAttachments(
            IList<Microsoft.Bot.Schema.Attachment>? attachments)
        {
            if (attachments == null || attachments.Count == 0)
            {
                return null;
            }

            var files = new List<FileAttachment>();

            foreach (var file in attachments)
            {
                if (string.IsNullOrEmpty(file.ContentUrl))
                    continue;

                using var httpClient = new HttpClient();
                using var stream = await httpClient.GetStreamAsync(new Uri(file.ContentUrl));
                var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                files.Add(new FileAttachment(ms, Path.GetFileName(file.ContentUrl)));
            }

            return files;
        }

        public override async Task CreateConversationAsync(
            string botAppId,
            string channelId,
            string serviceUrl,
            string audience,
            ConversationParameters conversationParameters,
            BotCallbackHandler callback,
            CancellationToken cancellationToken)
        {
            if (conversationParameters.ChannelData is not IDMChannel dm)
            {
                throw new NullReferenceException(nameof(dm));
            }

            var userAccount = new ConversationAccount(
                isGroup: false,
                conversationType: "DM",
                id: dm.Recipient.Id.ToString(),
                name: dm.Recipient.Username,
                aadObjectId: null,
                role: "user",
                tenantId: null
                );

            var members = new List<ChannelAccount>(conversationParameters.Members)
            {
                conversationParameters.Bot
            };
            var conversationUpdate = new Activity(
                conversation: userAccount,
                type: ActivityTypes.ConversationUpdate,
                id: Guid.NewGuid().ToString(),
                channelId: channelId,
                membersAdded: members,
                membersRemoved: new List<ChannelAccount>(),
                recipient: members[1],
                from: members[0],
                locale: "en-US"
                );

            var claims = new ClaimsIdentity();

            await ProcessActivityAsync(claims, conversationUpdate, callback, cancellationToken);
        }

        public override async Task<InvokeResponse> ProcessActivityAsync(
            ClaimsIdentity claimsIdentity,
            Activity activity,
            BotCallbackHandler callback,
            CancellationToken cancellationToken)
        {
            string json = JsonConvert.SerializeObject(activity, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            Logger.Log(LogLevel.Debug, "Incoming activity: {json}", json);

            var turnContext = new TurnContext(this, activity);
            await RunPipelineAsync(turnContext, callback, cancellationToken);
            return new InvokeResponse { Status = (int)HttpStatusCode.OK };
        }

        /// <summary>
        /// Starts a new Conversation or continues one if found in cache for the author of the message
        /// </summary>
        /// <param name="dm">Direct message channel found where the message came from</param>
        /// <param name="callbackHandler">Chat bot handler processing the message activity</param>
        /// <param name="message">Message sent in the dm channel</param>
        /// <returns></returns>
        public async Task CreateOrContinueDiscordConversationAsync(
            IDMChannel dm,
            BotCallbackHandler callbackHandler,
            SocketMessage message)
        {
            if (ActiveConversations.TryGetValue(dm.Recipient.Id.ToString(), out var conversation))
            {
                var messageActivity = GetMessageToBot(dm, message);
                messageActivity.InputHint = null;

                MessageCachingService.AddRecievedMessageActivity(messageActivity);
                await ProcessActivityAsync(new ClaimsIdentity(), messageActivity, callbackHandler, default);
            }
            else
            {
                await StartDiscordConversationAsync(dm.Recipient, callbackHandler);
            }
        }

        /// <summary>
        /// Start a new chat bot conversation with the given user, using the discord channel
        /// </summary>
        /// <param name="user">Target user to chat with</param>
        /// <param name="callbackHandler">Bot handler for the converstion</param>
        /// <param name="initialMessage">Optional starting message from the user</param>
        /// <returns>Queued work</returns>
        public async Task StartDiscordConversationAsync(
            IUser user,
            BotCallbackHandler callbackHandler)
        {
            var conversationParameters = await GetConversationParametersAsync(user);

            await CreateConversationAsync(
                botAppId: "",
                ChannelId,
                serviceUrl: "",
                audience: "",
                conversationParameters: conversationParameters,
                callback: callbackHandler,
                cancellationToken: default);
        }

        /// <summary>
        /// Creates a ChannelAccount from the given discord user
        /// </summary>
        /// <param name="user">Discord user information</param>
        /// <returns>ChannelAccount based on the discord information</returns>
        private ChannelAccount GetChannelAccount(IUser user)
        {
            return new ChannelAccount
            {
                Id = user.Id.ToString(),
                Name = user.Username,
                Role = user.Id == DiscordBot.Client.CurrentUser.Id ? RoleTypes.Bot : RoleTypes.User
            };
        }

        /// <summary>
        /// Creates conversation starting paramerters with the given user information
        /// </summary>
        /// <param name="user">User starting the conversation</param>
        /// <param name="initialMessage">Optional initial message for the bot</param>
        /// <returns>ConversationParameters containing basic information</returns>
        private async Task<ConversationParameters> GetConversationParametersAsync(IUser user)
        {
            var dm = await user.CreateDMChannelAsync();
            var parameters = new ConversationParameters()
            {
                Bot = GetChannelAccount(DiscordBot.Client.CurrentUser),
                ChannelData = dm,
                IsGroup = false,
                Members = new List<ChannelAccount>() { GetChannelAccount(user) }
            };

            return parameters;
        }

        /// <summary>
        /// Creates a message activity for sending a message to the chat bot
        /// </summary>
        /// <param name="dm">Discord dm channel where the user message came from</param>
        /// <param name="message">Message text to be sent to the chat bot</param>
        /// <returns>Activity to be sent to the chat bot</returns>
        private Activity GetMessageToBot(IDMChannel dm, SocketMessage message)
        {
            var activity = MessageFactory.Text(message.Content, message.Content);
            activity.ChannelId = ChannelId;
            activity.Id = message.Id.ToString();
            activity.From = GetChannelAccount(dm.Recipient);
            activity.Recipient = GetChannelAccount(DiscordBot.Client.CurrentUser);
            activity.Locale = "en-US";
            activity.TextFormat = TextFormatTypes.Plain;
            activity.Timestamp = DateTime.UtcNow;
            activity.Entities = new List<Entity>()
            {
                new Entity()
                {
                    Type = "ClientCapabilities",
                    Properties = JObject.FromObject(new
                    {
                        requiresBotState = true,
                        supportsListening = true,
                        supportsTts = true,
                    })
                }
            };

            if (ActiveConversations.TryGetValue(dm.Recipient.Id.ToString(), out var conversation))
            {
                activity.Conversation = new ConversationAccount
                {
                    Id = conversation.Conversation.Id
                };
            }

            return activity;
        }
    }
}