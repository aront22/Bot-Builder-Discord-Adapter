// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.18.1

using DiscordAdapter.Extensions;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CoreBotWithTests.Bots
{
    public class DialogAndWelcomeBot<T> : DialogBot<T>
        where T : Dialog
    {
        public DialogAndWelcomeBot(ConversationState conversationState, UserState userState, T dialog, ILogger<DialogBot<T>> logger)
            : base(conversationState, userState, dialog, logger)
        {
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                // Greet anyone that was not the target (recipient) of this message.
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    string welcomeText = "Welcome to Bot Framework!";
                    var response = MessageFactory.Text(text: welcomeText, ssml: welcomeText);
                    await turnContext.SendActivityAsync(response, cancellationToken);
                    await Dialog.RunAsync(turnContext, ConversationState.CreateProperty<DialogState>("DialogState"), cancellationToken);
                }
            }
        }

        protected async override Task OnReactionsAddedAsync(IList<MessageReaction> messageReactions, ITurnContext<IMessageReactionActivity> turnContext, CancellationToken cancellationToken)
        {
            var activity = new Activity().CreateMessageReactionActivity(
                turnContext.Activity.ReplyToId,
                turnContext.Activity.Conversation
                );

            activity.AddReaction(new MessageReaction { Type = "😂" });

            await turnContext.SendActivityAsync(activity);
        }

        protected override async Task OnReactionsRemovedAsync(
            IList<MessageReaction> messageReactions,
            ITurnContext<IMessageReactionActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var activity = new Activity().CreateMessageReactionActivity(
                turnContext.Activity.ReplyToId,
                turnContext.Activity.Conversation
                );

            activity.AddReaction(new MessageReaction { Type = "❤️" });
            activity.RemoveReaction(new MessageReaction { Type = "❤️" });

            await turnContext.SendActivityAsync(activity);
        }

        protected override async Task OnMessageDeleteActivityAsync(
            ITurnContext<IMessageDeleteActivity> turnContext,
            CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync($"You deleted: {turnContext.Activity.Id}");
        }

        protected override async Task OnMessageUpdateActivityAsync(
            ITurnContext<IMessageUpdateActivity> turnContext,
            CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync($"You updated: {turnContext.Activity.Id} - {turnContext.Activity.Text}");
        }

        protected override async Task OnTypingActivityAsync(
            ITurnContext<ITypingActivity> turnContext,
            CancellationToken cancellationToken)
        {
            var activity = turnContext.Activity as Activity;
            activity.ReverseSenderAndRecipient();
            await turnContext.SendActivityAsync(activity, default);
        }
    }
}
