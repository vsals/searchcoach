﻿// <copyright file="ActivityMiddleware.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.SearchCoach.Bot
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder;
    using Microsoft.Bot.Schema;
    using Microsoft.Extensions.Localization;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.Teams.Apps.SearchCoach.Models.Configuration;

    /// <summary>
    /// A class that represents middle-ware that can operate on incoming activities.
    /// </summary>
    public class ActivityMiddleware : IMiddleware
    {
        /// <summary>
        /// Represents a set of key/value application configuration properties for Bot.
        /// </summary>
        private readonly IOptions<BotSettings> options;

        /// <summary>
        /// Logger implementation to send logs to the logger service.
        /// </summary>
        private readonly ILogger<ActivityMiddleware> logger;

        /// <summary>
        /// The current cultures' string localizer.
        /// </summary>
        private readonly IStringLocalizer<Strings> localizer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityMiddleware"/> class.
        /// </summary>
        /// <param name="options"> A set of key/value application configuration properties.</param>
        /// <param name="logger">Logger implementation to send logs to the logger service.</param>
        /// <param name="localizer">The current cultures' string localizer.</param>
        public ActivityMiddleware(IOptions<BotSettings> options, ILogger<ActivityMiddleware> logger, IStringLocalizer<Strings> localizer)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.logger = logger;
            this.localizer = localizer;
        }

        /// <summary>
        ///  Processes an incoming activity in middle-ware.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="next">The delegate to call to continue the bot middle-ware pipeline.</param>
        /// <param name="cancellationToken"> A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns><see cref="Task"/> A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// Middle-ware calls the next delegate to pass control to the next middle-ware in
        /// the pipeline. If middle-ware doesn’t call the next delegate, the adapter does
        /// not call any of the subsequent middle-ware's request handlers or the bot’s receive
        /// handler, and the pipeline short circuits.
        /// The turnContext provides information about the incoming activity, and other data
        /// needed to process the activity.
        /// </remarks>
        public async Task OnTurnAsync(ITurnContext turnContext, NextDelegate next, CancellationToken cancellationToken = default)
        {
            turnContext = turnContext ?? throw new ArgumentNullException(nameof(turnContext));
            next = next ?? throw new ArgumentNullException(nameof(next));

            if (turnContext.Activity?.Type != ActivityTypes.Event && !this.IsActivityFromExpectedTenant(turnContext))
            {
                this.logger.LogWarning($"Activity type: {turnContext.Activity?.Type} Unexpected tenant id {turnContext.Activity?.Conversation?.TenantId}");

                if (turnContext.Activity?.Type == ActivityTypes.Message)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(this.localizer.GetString("InvalidTenantText")), cancellationToken);
                }
            }
            else
            {
                await next(cancellationToken);
            }
        }

        /// <summary>
        /// Verify if the tenant id in the message is the same tenant id used when application was configured.
        /// </summary>
        /// <param name="turnContext">Context object containing information cached for a single turn of conversation with a user.</param>
        /// <returns>True if context is from expected tenant else false.</returns>
        private bool IsActivityFromExpectedTenant(ITurnContext turnContext)
        {
            return turnContext.Activity?.Conversation?.TenantId == this.options.Value.TenantId;
        }
    }
}