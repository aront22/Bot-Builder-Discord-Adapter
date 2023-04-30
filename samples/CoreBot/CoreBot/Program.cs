// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.18.1

using CoreBotWithTests;
using CoreBotWithTests.Bots;
using CoreBotWithTests.Dialogs;
using DiscordAdapter.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((hostingContext, services, loggerConfiguration) =>
{
    loggerConfiguration
        .MinimumLevel.Debug()
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Host.ConfigureServices((hostContext, services) =>
{
    services.AddHttpClient()
            .AddControllers()
            .AddNewtonsoftJson();

    // Create the storage we'll be using for User and Conversation state. (Memory is great for testing purposes.)
    services.AddSingleton<IStorage, MemoryStorage>();

    // Create the User state. (Used in this bot's Dialog implementation.)
    services.AddSingleton<UserState>();

    // Create the Conversation state. (Used by the Dialog system itself.)
    services.AddSingleton<ConversationState>();

    // Register LUIS recognizer
    services.AddSingleton<FlightBookingRecognizer>();

    // Register the BookingDialog.
    services.AddSingleton<BookingDialog>();

    // The MainDialog that will be run by the bot.
    services.AddSingleton<MainDialog>();

    // Create the bot as a transient. In this case the ASP Controller is expecting an IBot.
    services.AddTransient<IBot, DialogAndWelcomeBot<MainDialog>>();
});

// Add and configure all Discord Adapter related classes with the default options
builder.Host.AddDefaultDiscord();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseDefaultFiles()
    .UseStaticFiles()
    .UseWebSockets()
    .UseRouting()
    .UseAuthorization()
    .UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
    });

var adapter = app.Services.GetService<DiscordAdapter.DiscordAdapter>();

app.UseHttpsRedirection();

app.Run();