using Microsoft.Bot.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordAdapter
{
    public class DiscordAdapterOptions
    {
        public const string AdapterOptions = nameof(DiscordAdapterOptions);

        public Func<IServiceProvider, IBot> ChatBotFactory { get; set; }
             = (services) => services.GetRequiredService<IBot>();
    }
}
