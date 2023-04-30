# CoreBot with Discord Adapter

You should already be familiar with all [Bot Framework](https://dev.botframework.com/) configuration, setup and concepts, this document will only describe the Discord adapter related code and concepts.

## Important concepts of Discord and the adapter

Discord uses a websocket connection to send events to bot accounts. Not like other adapters, the Discord adapter does not implement the `IBotFrameworkHttpAdapter`, rather it uses a `DiscordSocketClient` internally to communicate with the Discord channel through the Discord API. When using the adapter your bot needs to send activities with the `TurnContext`'s functions and the adapter will handle sending the appropriate action to Discord.

## Using the adapter
### Startup config

When using the Discord adapter the easiest way to set it up is to use the extension function on the host builder:

```csharp
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
...
// other Program.cs setup stuff
...
// Add and configure all Discord Adapter related classes with the default options
builder.Host.AddDefaultDiscord();
```

This will add a `DiscordSocketClient` to the DI and a default `IDiscordBot` implementation with minimal setup and logging. This will be more then enough in most cases if your goal is to have conversation on Discord with your chatbot.

The Discord bot needs a token to connect to the Discord API, you need to have a `Token` key defined under the `DiscordBotOptions` key like this in the `appsettings.json` :
```json
"DiscordBotOptions": {
    "Token": "your discord bot token"
  }
```

or an Environment variable under this key:
```
DiscordBotOptions__Token
```

or any other way you like, but it has to be available in the `IConfiguration`.

The adapter is expecting to have an `IBot` implementation already registered in the DI container. 

The adapter will start new conversations when a user sends a message to the bot for the first time. This can be disabled in the `DiscordAdapterOptions` by setting the `StartConversationOnMessageReceived` property to `false`. This way you will have to use the `StartDiscordConversationAsync` function on the adapter to start a conversations manually.

 > This function need an `IUser` parameter which you can obtain using the Discord.Net library. The easiest way is to get the `DiscordSocketClient` from the DI container and use the `GetUserAsync(ulong discordId)` function to get the user's object. This method only recommended when custom logic is needed to perform checks to start the conversation.

You can pass a `DiscordAdapterOptions` instance to any of the host builder extension functions when adding the discord adapter.

The `DiscordBot` is a hosted service and it takes time to initialize and connect to the Discord Gateway, sending any `Activity` from your `IBot` implementation to the `DiscordAdapter` will throw an `ApplicationException` as the bot cannot send API requests before connecting to the Discord API.

### More control over setup

The adapter uses the following DiscordSocketClient events to handle it's functionality:
```csharp
ButtonExecuted // User clicked a button
MessageReceived // User has sent a message to the bot
ReactionAdded // User added a reaction to a message
ReactionRemoved // User have removed a reaction from a message
MessageDeleted // User has deleted a message 
MessageUpdated // User has edited a message
UserIsTyping // User has started typing
```

You can create a custom implementation of the `DiscordBot` class by inheriting from it and you can override functions like `CheckMessageReceivedPreconditions` veto the handling of these events. You can perform any custom logic in these functions and by returning `false` you can prevent the adapter from handling the event and sending the related `Activity` to your `IBot` implementation.
If you are using a custom `IDiscordBot` implementation make sure to register the adapter with the `AddDiscord<YourDiscordBot>()` extension method on the host builder.

You can provide a custom function in the `DiscordAdapterOptions` for resolving an `IBot` to handle activities, by default it will get the first from the DI container. This can be useful if you want to handle different activities by different bots for some reason. 
> My recommendation is to have one DiscordBot and one chat bot in a single ASP .NET application, and create more apps if you want to use multiple bots. 