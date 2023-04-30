using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DiscordAdapter.AspNetCore
{
    public static class IHostBuilderExtensions
    {
        private static IServiceCollection AddDiscord<TDiscordBot>(
            this IServiceCollection services,
            DiscordAdapterOptions? config = default,
            Func<IServiceProvider, DiscordSocketClient>? socketClientFactory = default)
        where TDiscordBot : class, IDiscordBot
        {
            services.AddDiscordSocketClient(socketClientFactory);
            services.AddDiscordAdapter(config);
            services.AddDiscordBot<TDiscordBot>();

            return services;
        }

        /// <summary>
        /// Registers the <see cref="DiscordSocketClient"/> with the <see cref="DiscordSocketConfig"/> already registered in the service collection
        /// or the one found in the <see cref="IConfiguration"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="socketClientFactory">Factory function used for creating the <see cref="DiscordSocketClient"/> instance.</param>
        /// <returns></returns>
        public static IServiceCollection AddDiscordSocketClient(
            this IServiceCollection services,
            Func<IServiceProvider, DiscordSocketClient>? socketClientFactory = default)
        {
            socketClientFactory ??= (services) =>
            {
                var config = services.GetService<DiscordSocketConfig>()
                              ?? services.GetRequiredService<IOptions<DiscordBotOptions>>().Value.SocketConfig;
                return new DiscordSocketClient(config);
            };
            var socketClient = socketClientFactory(services.BuildServiceProvider());
            services.AddSingleton<DiscordSocketClient>(socketClient);
            return services;
        }

        /// <summary>
        /// Registers the discord adapter class with the given or default adapter settings
        /// </summary>
        /// <param name="services"></param>
        /// <param name="config">Discord adapter settings</param>
        /// <returns></returns>
        public static IServiceCollection AddDiscordAdapter(
            this IServiceCollection services,
            DiscordAdapterOptions? config = default)
        {
            services.AddSingleton<DiscordAdapter>();
            services.AddSingleton((services) =>
            {
                return config ?? new DiscordAdapterOptions();
            });
            return services;
        }

        /// <summary>
        /// Register the template parameter as a singleton <see cref="IDiscordBot"/> implementation and as a <see cref="IHostedService"/> service
        /// </summary>
        /// <typeparam name="TDiscordBot"></typeparam>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddDiscordBot<TDiscordBot>(
            this IServiceCollection services)
        where TDiscordBot : class, IDiscordBot
        {
            services.AddSingleton<IDiscordBot, TDiscordBot>();
            services.AddHostedService<TDiscordBot>(sp => (TDiscordBot)sp.GetRequiredService<IDiscordBot>());
            return services;
        }

        /// <summary>
        /// Registers the discord adapter with configurable settings
        /// </summary>
        /// <typeparam name="TDiscordBot">Your DiscordBot type implementing the <see cref="IDiscordBot"/> interface</typeparam>
        /// <param name="builder"></param>
        /// <param name="config">Discord adapter settings</param>
        /// <param name="socketClientFactory">Function used for creating the <see cref="DiscordSocketClient"/> used for communication with discord.</param>
        /// <returns></returns>
        public static IHostBuilder AddDiscord<TDiscordBot>(
            this IHostBuilder builder,
            DiscordAdapterOptions? config = default,
            Func<IServiceProvider, DiscordSocketClient>? socketClientFactory = default)
            where TDiscordBot : class, IDiscordBot
        {
            builder.ConfigureServices((hostContext, services) =>
            {
                services.Configure<DiscordBotOptions>(
                    hostContext.Configuration.GetSection(DiscordBotOptions.BotOptions));
                services.AddDiscord<TDiscordBot>(config, socketClientFactory);
            }).ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddEnvironmentVariables();
            });
            return builder;
        }

        /// <summary>
        /// Registers the discord adapter with the default library implementations and settings
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="config">Adapter options for configuring</param>
        /// <returns></returns>
        public static IHostBuilder AddDefaultDiscord(this IHostBuilder builder, DiscordAdapterOptions? config = default)
        {
            return builder.AddDiscord<DiscordBot>(config);
        }

    }
}
