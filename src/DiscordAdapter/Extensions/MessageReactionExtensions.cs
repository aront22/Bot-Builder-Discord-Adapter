using Discord;
using Microsoft.Bot.Schema;

namespace DiscordAdapter.Extensions
{
    public static class MessageReactionExtensions
    {
        public static IEmote ToDiscordEmote(this MessageReaction messageReaction) 
        {
            if (Emote.TryParse(messageReaction.Type, out var guildEmote))
            {
                return guildEmote;
            }

            if (Emoji.TryParse(messageReaction.Type, out var emoji))
            {
                return emoji;
            }

            return new Emoji(messageReaction.Type);
        }
    }
}
