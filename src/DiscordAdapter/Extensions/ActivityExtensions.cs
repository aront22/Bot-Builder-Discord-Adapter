using Discord;
using Microsoft.Bot.Schema;

namespace DiscordAdapter.Extensions
{
    public static class ActivityExtensions
    {
        public static Activity CreateMessageReactionActivity(
            this Activity activity,
            string originalActivityId,
            ConversationAccount conversation,
            ChannelAccount? reactingUser = null
            )
        {
            activity.Type = ActivityTypes.MessageReaction;
            activity.Id = originalActivityId;
            activity.Conversation = conversation;
            activity.From = reactingUser;
            activity.ReplyToId = originalActivityId;

            return activity;
        }

        public static Activity SetAddedReactions(
            this Activity activity,
            IList<IEmote> reactions)
        {
            activity.ReactionsAdded = reactions.Select(e =>
            {
                return new MessageReaction() { Type = e.Name };
            }).ToList();
            return activity;
        }

        public static Activity SetAddedReaction(
            this Activity activity,
            IEmote reaction)
        {
            activity.ReactionsAdded = new List<MessageReaction>() { new MessageReaction { Type = reaction.Name } };
            return activity;
        }

        public static Activity SetRemovedReactions(
            this Activity activity,
            IList<IEmote> reactions)
        {
            activity.ReactionsRemoved = reactions.Select(e =>
            {
                return new MessageReaction() { Type = e.Name };
            }).ToList();
            return activity;
        }

        public static Activity SetRemovedReaction(
            this Activity activity,
            IEmote reaction)
        {
            activity.ReactionsRemoved = new List<MessageReaction>() { new MessageReaction { Type = reaction.Name } };
            return activity;
        }

        public static Activity AddReaction(
            this Activity activity,
            MessageReaction reaction)
        {
            activity.ReactionsAdded ??= new List<MessageReaction>();
            activity.ReactionsAdded.Add(reaction);
            return activity;
        }

        public static Activity RemoveReaction(
            this Activity activity,
            MessageReaction reaction)
        {
            activity.ReactionsRemoved ??= new List<MessageReaction>();
            activity.ReactionsRemoved.Add(reaction);
            return activity;
        }

        public static Activity ReverseSenderAndRecipient(this Activity activity)
        {
            var from = activity.From;
            var recipient = activity.Recipient;
            activity.From = recipient;
            activity.Recipient = from;
            return activity;
        }
    }
}
