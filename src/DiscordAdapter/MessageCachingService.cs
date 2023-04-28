using Discord;
using Discord.WebSocket;
using DiscordAdapter.Extensions;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DiscordAdapter
{
    /// <summary>
    /// Caching discord messages and bot framework activities for future references
    /// </summary>
    public class MessageCachingService
    {
        /// <summary>
        /// Messages sent in DM channels, key is the discord message id.
        /// </summary>
        private ConcurrentDictionary<ulong, IUserMessage> MessageCache { get; set; } = new();
        /// <summary>
        /// (Outgoing) Activities sent to the user from the bot. Bot => Discord => User
        /// </summary>
        private ConcurrentDictionary<string, IMessageActivity> SentMessageActivities { get; set; } = new();
        /// <summary>
        /// (Incoming) Activities sent to the bot by from the user. User => Discord => Bot
        /// </summary>
        private ConcurrentDictionary<string, IMessageActivity> RecivedMessageActivities { get; set; } = new();

        private readonly ILogger logger;
        private readonly DiscordSocketClient client;
        private DateTimeOffset lastPurge = DateTimeOffset.UtcNow;
        private readonly TimeSpan purgeIntervall = TimeSpan.FromHours(1);

        public MessageCachingService(DiscordSocketClient client, ILogger<MessageCachingService> logger)
        {
            this.logger = logger;
            this.client = client;
            this.client.MessageReceived += HandleMessageRecieved;
        }

        private Task HandleMessageRecieved(SocketMessage message)
        {
            // Only cache messages sent in DMs by users. Bot messages will be added manually from the DicordAdapter.
            if (message.Channel is IDMChannel dm && message is IUserMessage msg && !msg.Author.IsBot)
            {
                MessageCache.AddOrUpdate(msg.Id, msg, (id, m) => msg);
                logger.LogDebug("[Message Cache]: Cached user message {Id}", msg.Id);
            }
            if (DateTimeOffset.UtcNow.Subtract(lastPurge.ToUniversalTime()) > purgeIntervall)
            {
                _ = Task.Run(() => PurgeOldMessages(DateTimeOffset.UtcNow));
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// Add a DM message sent by the bot.
        /// </summary>
        /// <param name="message">Message sent by the bot.</param>
        public void AddDiscordBotMessage(IUserMessage message)
        {
            if (message.Channel is IDMChannel dm && message is IUserMessage msg)
            {
                MessageCache.AddOrUpdate(msg.Id, msg, (id, m) => msg);
                logger.LogDebug("[Message Cache]: Cached bot message {Id}", msg.Id);
            }
        }

        /// <summary>
        /// Get a message from the cache by it's id.
        /// </summary>
        /// <param name="messageId">Id of the cached message.</param>
        /// <returns>The cached message or null if its not cached.</returns>
        public IUserMessage? GetUserMessage(ulong messageId)
        {
            if (!MessageCache.TryGetValue(messageId, out var message))
            {
                logger.LogDebug("[Message Cache]: Message {messageId} not found in cache.", messageId);
                return null;
            }

            return message;
        }

        /// <summary>
        /// Deletes all messages from the cache sent before the given DateTimeOffset.
        /// </summary>
        /// <param name="olderThan"></param>
        private void PurgeOldMessages(DateTimeOffset olderThan)
        {
            long unixSeconds = olderThan.ToUniversalTime().ToUnixTimeSeconds();
            var old = MessageCache.Where(kv => kv.Value.Timestamp.ToUniversalTime().ToUnixTimeSeconds() < unixSeconds).ToList();
            foreach (var kv in old)
            {
                MessageCache.Remove(kv.Key, out var _);
            }
            lastPurge = DateTimeOffset.UtcNow;
            logger.LogDebug("[Message Cache]: Message cache purged, deleted {count} messages.", old.Count);
        }

        /// <summary>
        /// Add a sent (outgoing) activity to the cache
        /// </summary>
        /// <param name="activity">Activity to cache</param>
        public void AddSentMessageActivity(IMessageActivity activity)
        {
            SentMessageActivities.AddOrUpdate(activity.Id, activity, (id, a) => activity);
            logger.LogDebug("[Message Cache]: Cached sent message activity: {activityId}", activity.Id);
        }

        /// <summary>
        /// Add a recieved (incoming) activity to the cache
        /// </summary>
        /// <param name="activity">Activity to cache</param>
        public void AddRecievedMessageActivity(IMessageActivity activity)
        {
            RecivedMessageActivities.AddOrUpdate(activity.Id, activity, (id, a) => activity);
            logger.LogDebug("[Message Cache]: Cached recieved message activity: {activityId}", activity.Id);
        }

        /// <summary>
        /// Gets a cached sent activity
        /// </summary>
        /// <param name="activityId">id of the actvity.</param>
        /// <returns>Message Activity or null if not cached</returns>
        public IMessageActivity? GetSentMessageActivity(string activityId)
        {
            if (!SentMessageActivities.TryGetValue(activityId, out var activity))
            {
                logger.LogDebug("[Message Cache]: Sent Activity {activityId} not found in cache.", activityId);
                return null;
            }

            return activity;
        }

        /// <summary>
        /// Gets a cached recieved activity
        /// </summary>
        /// <param name="activityId">id of the activity</param>
        /// <returns>Message Actvity or null if not cached</returns>
        public IMessageActivity? GetRecievedMessageActivity(string activityId)
        {
            if (!RecivedMessageActivities.TryGetValue(activityId, out var activity))
            {
                logger.LogDebug("[Message Cache]: Recieved Activity {activityId} not found in cache.", activityId);
                return null;
            }

            return activity;
        }

        /// <summary>
        /// Clears the cached messages and actvities related to the given user.
        /// </summary>
        /// <param name="userId">Targeted user's id</param>
        public void ClearCacheForUser(ulong userId)
        {
            var t1 = Task.Run(() =>
            {
                var toRemove = MessageCache.Where(m => m.Value.Author.Id == userId ||
                                                      (m.Value.Channel as IDMChannel)!.Recipient.Id == userId).ToList();
                foreach (var message in toRemove)
                {
                    MessageCache.Remove(message.Key, out var _);
                }
                logger.Log(LogLevel.Debug, "Removed {count} messages.", toRemove.Count);
            });

            var t2 = Task.Run(() =>
            {
                var sent = SentMessageActivities.Where(a => a.Value.Conversation.Id.ToUInt64() == userId).ToList();
                foreach (var activity in sent)
                {
                    SentMessageActivities.Remove(activity.Key, out var _);
                }
                logger.Log(LogLevel.Debug, "Removed {count} sent actvities.", sent.Count);
            });

            var t3 = Task.Run(() =>
            {
                var recieved = RecivedMessageActivities.Where(a => a.Value.Conversation.Id.ToUInt64() == userId).ToList();
                foreach (var activity in recieved)
                {
                    RecivedMessageActivities.Remove(activity.Key, out var _);
                }
                logger.Log(LogLevel.Debug, "Removed {count} recieved actvities.", recieved.Count);
            });

            Task.WaitAll(t1, t2, t3);
        }
    }
}
