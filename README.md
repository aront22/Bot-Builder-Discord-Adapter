# Discord Adapter for Microsoft Bot Framework v4

With this library you can connect you Bot Framework v4 chat bot to discord. 

## Features

- Sending and receiving messages
- Editing messages
- Deleting messages
- Adding and removing reactions
- Triggering typing indicator
- Sending buttons with choice input options

**Only Direct Message channels are supported! Servers and Groups are NOT!**

**Adaptive cards not supported!**

## Setup

### Creating a Discord application and bot user

To use the adapter first you need a Discord account and you need to register an application the [Discord developer portal](https://discord.com/developers/applications). You need to create a new application with a bot user. (As of late all new apps come with a bot user by default.) Click the "New Application" button on the top right, give it a nice name, once created navigate to the "Bot" tab on the left and copy the token or click the reset button if its not shown. This token must be kept secret or others will be able to control your bot! You will need to add this token to your project config shown later.

### Using the adapter in a ASP .NET project

Check out the samples folder for an example covering almost all features of the adapter [here.](/samples/CoreBot/CoreBot/README.md)