using Discord;
using Discord.WebSocket;

namespace DiscordAdapter
{
    public class DiscordBotOptions
    {
        public const string BotOptions = nameof(DiscordBotOptions);

        public string Token { get; set; }

        public DiscordSocketConfig SocketConfig { get; set; } = new DiscordSocketConfig
        {
            AlwaysDownloadUsers = true,
            GatewayIntents = GatewayIntents.All,
            LargeThreshold = 250,
            LogLevel = LogSeverity.Verbose,
            MessageCacheSize = 100,
            UseInteractionSnowflakeDate = true,
            UseSystemClock = false,
        };
    }
}
