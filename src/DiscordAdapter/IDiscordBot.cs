using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;

namespace DiscordAdapter
{
    public interface IDiscordBot : IHostedService
    {
        /// <summary>
        /// Discord client used for recieving message events from Discord
        /// </summary>
        public DiscordSocketClient Client { get; }

        /// <summary>
        /// True if the bot has started, successfully connected to discord and has an active connection
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// Send a message to the given user
        /// </summary>
        /// <param name="userId">Id of the targeted user</param>
        /// <param name="message">text of the message</param>
        /// <param name="components">Message components, like buttons</param>
        /// <param name="embed">Discord embed</param>
        /// <param name="attachments">File attachments</param>
        /// <returns></returns>
        public Task<IUserMessage> SendMessageToUser(
            ulong userId,
            string message,
            MessageComponent? components = null,
            Embed? embed = null,
            IEnumerable<FileAttachment>? attachments = null);

        /// <summary>
        /// Check custom precondtions before execution of the MessageReceived DiscordAdapter code.
        /// Return false to prevent the DiscordAdapter code from running.
        /// </summary>
        /// <param name="message">The message that was recieved</param>
        /// <returns></returns>
        public Task<bool> CheckMessageReceivedPreconditions(SocketMessage message) => Task.FromResult(true);

        /// <summary>
        /// Check custom precondtions before execution of the ButtonClicked DiscordAdapter code.
        /// Return false to prevent the DiscordAdapter code from running.
        /// </summary>
        /// <param name="button">The button that was pressed</param>
        /// <returns></returns>
        public Task<bool> CheckButtonClickedPreconditions(SocketMessageComponent button) => Task.FromResult(true);

        /// <summary>
        /// Check custom precondtions before execution of the ReactionAdded DiscordAdapter code.
        /// Return false to prevent the DiscordAdapter code from running.
        /// </summary>
        /// <param name="message">The message that the reaction was added to</param>
        /// <param name="channel">The channel where the message was sent</param>
        /// <param name="reaction">>The reaction that was added</param>
        /// <returns></returns>
        public Task<bool> CheckReactionAddedPreconditions(
            Cacheable<IUserMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel,
            SocketReaction reaction) => Task.FromResult(true);

        /// <summary>
        /// Check custom precondtions before execution of the ReactionRemoved DiscordAdapter code.
        /// Return false to prevent the DiscordAdapter code from running.
        /// </summary>
        /// <param name="message">The message that the reaction was removed from</param>
        /// <param name="channel">The channel where the message was sent</param>
        /// <param name="reaction">The reaction that was removed</param>
        /// <returns></returns>
        public Task<bool> CheckReactionRemovedPrecondtions(
            Cacheable<IUserMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel,
            SocketReaction reaction) => Task.FromResult(true);

        /// <summary>
        /// Check custom precondtions before execution of the MessageDeleted DiscordAdapter code.
        /// Return false to prevent the DiscordAdapter code from running.
        /// </summary>
        /// <param name="message">The message that was deleted.</param>
        /// <param name="channel">Channel where the message was deleted</param>
        /// <returns></returns>
        public Task<bool> CheckMessageDeletedPrecondtions(
            Cacheable<IMessage, ulong> message,
            Cacheable<IMessageChannel, ulong> channel) => Task.FromResult(true);

        /// <summary>
        /// Check custom precondtions before execution of the MessageUpdated DiscordAdapter code.
        /// Return false to prevent the DiscordAdapter code from running.
        /// </summary>
        /// <param name="before">Message state before the update</param>
        /// <param name="after">Message state after the update</param>
        /// <param name="channel">Channel where the message was originally sent</param>
        /// <returns></returns>
        public Task<bool> CheckMessageUpdatedPrecondtions(
            Cacheable<IMessage, ulong> before,
            SocketMessage after,
            ISocketMessageChannel channel) => Task.FromResult(true);

        /// <summary>
        /// Check custom precondtions before execution of the UserIsTyping DiscordAdapter code.
        /// Return false to prevent the DiscordAdapter code from running.
        /// </summary>
        /// <param name="user">User who started typing</param>
        /// <param name="channel">Channel where the user is typing</param>
        /// <returns></returns>
        public Task<bool> CheckUserIsTypingPrecondtions(
            Cacheable<IUser, ulong> user,
            Cacheable<IMessageChannel, ulong> channel) => Task.FromResult(true);
    }
}
