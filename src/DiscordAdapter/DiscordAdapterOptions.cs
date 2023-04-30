using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordAdapter
{
    public class DiscordAdapterOptions
    {
        public const string AdapterOptions = nameof(DiscordAdapterOptions);

        public bool StartConversationOnMessageReceived { get; set; } = true;

        /// <summary>
        /// Function for getting an IBot implementation to handle actvities based on your logic.
        /// By default it gets the first IBot implementation from the DI container.
        /// </summary>
        public Func<IServiceProvider, Activity?, IBot> ChatBotFactory { get; set; }
             = (services, activity) => services.GetRequiredService<IBot>();
    }
}
