using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordAdapter
{
    /// <summary>
    /// Default implementation of a discord bot for the DiscordAdapter.
    /// Handles connecting to Discord and basic logging.
    /// </summary>
    public class DiscordBot : IHostedService, IDiscordBot
    {
        #region dependecies
        private readonly DiscordSocketClient client;
        protected readonly DiscordBotOptions options;
        protected readonly ILogger logger;
        #endregion

        #region private vars
        private bool isInitialized = false;
        #endregion

        #region interface properties
        public DiscordSocketClient Client => client;
        public bool IsConnected => isInitialized && client.ConnectionState == Discord.ConnectionState.Connected;
        #endregion

        public DiscordBot(
            DiscordSocketClient client,
            IOptions<DiscordBotOptions> options,
            ILogger<DiscordBot> logger)
        {
            this.client = client;
            this.options = options.Value;
            this.logger = logger;
        }

        #region helper functions
        private static LogLevel MapLogLevel(LogSeverity severity) => severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information,
        };

        protected virtual void RegisterEvents()
        {
            client.Log += HandleLog;
            client.Ready += HandleReady;
        }
        #endregion

        #region event handlers
        protected virtual Task HandleReady()
        {
            isInitialized = true;
            return Task.CompletedTask;
        }

        protected virtual Task HandleLog(LogMessage log)
        {
            logger.Log(MapLogLevel(log.Severity), log.Exception, "[{logSource}]: {logMessage}", log.Source, log.Message);
            return Task.CompletedTask;
        }
        #endregion

        #region IDiscordBot functions
        public async Task<IUserMessage> SendMessageToUser(
            ulong userId,
            string message,
            MessageComponent? components = null,
            Embed? embed = null,
            IEnumerable<FileAttachment>? attachments = null)
        {
            IUser user = await client.GetUserAsync(userId);
            IDMChannel dm = await user.CreateDMChannelAsync();
            if (attachments == null || !attachments.Any())
            {
                return await dm.SendMessageAsync(
                text: message,
                embed: embed,
                components: components);
            }
            else
            {
                return await dm.SendFilesAsync(attachments: attachments, text: message, embed: embed, components: components);
            }
        }

        public virtual Task<bool> CheckMessageReceivedPreconditions(SocketMessage message) => Task.FromResult(true);
        public virtual Task<bool> CheckButtonClickedPreconditions(SocketMessageComponent button) => Task.FromResult(true);
        public virtual Task<bool> CheckReactionAddedPreconditions(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction) => Task.FromResult(true);
        public virtual Task<bool> CheckReactionRemovedPreconditions(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction) => Task.FromResult(true);
        public virtual Task<bool> CheckMessageDeletedPreconditions(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel) => Task.FromResult(true);
        public virtual Task<bool> CheckMessageUpdatedPreconditions(Cacheable<IMessage, ulong> before, SocketMessage after, ISocketMessageChannel channel) => Task.FromResult(true);
        public virtual Task<bool> CheckUserIsTypingPreconditions(Cacheable<IUser, ulong> user, Cacheable<IMessageChannel, ulong> channel) => Task.FromResult(true);
        #endregion

        #region hosted service functions
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(options.Token))
            {
                throw new ArgumentNullException(nameof(options.Token), "Discord bot token is required to connect to discord!");
            }

            RegisterEvents();

            await client.LoginAsync(TokenType.Bot, options.Token);
            await client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await client.StopAsync();
            await client.LogoutAsync();
        }
        #endregion
    }
}
