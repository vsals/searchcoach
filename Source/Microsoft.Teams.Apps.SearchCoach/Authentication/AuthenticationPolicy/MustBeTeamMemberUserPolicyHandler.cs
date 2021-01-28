﻿// <copyright file="MustBeTeamMemberUserPolicyHandler.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace Microsoft.Teams.Apps.SearchCoach.Authentication.AuthenticationPolicy
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Options;
    using Microsoft.Teams.Apps.SearchCoach.Common;
    using Microsoft.Teams.Apps.SearchCoach.Models.Configuration;
    using Microsoft.Teams.Apps.SearchCoach.Services.MicrosoftGraph.GroupMembers;

    /// <summary>
    /// This authorization handler is created to handle team's user policy.
    /// The class implements AuthorizationHandler for handling MustBeTeamMemberUserPolicyRequirement authorization.
    /// </summary>
    public class MustBeTeamMemberUserPolicyHandler : IAuthorizationHandler
    {
        /// <summary>
        /// Cache for storing authorization result.
        /// </summary>
        private readonly IMemoryCache memoryCache;

        /// <summary>
        /// A set of key/value application configuration properties for Activity settings.
        /// </summary>
        private readonly IOptions<BotSettings> botOptions;

        /// <summary>
        /// Instance of MemberValidationService to validate member.
        /// </summary>
        private readonly IMemberValidationService memberValidationService;

        /// <summary>
        /// Initializes a new instance of the <see cref="MustBeTeamMemberUserPolicyHandler"/> class.
        /// </summary>
        /// <param name="memoryCache">MemoryCache instance for caching authorization result.</param>
        /// <param name="botOptions">A set of key/value application configuration properties for activity handler.</param>
        /// <param name="memberValidationService">Provider to fetch group membership.</param>
        public MustBeTeamMemberUserPolicyHandler(
            IMemoryCache memoryCache,
            IOptions<BotSettings> botOptions,
            IMemberValidationService memberValidationService)
        {
            this.memoryCache = memoryCache;
            this.botOptions = botOptions ?? throw new ArgumentNullException(nameof(botOptions));
            this.memberValidationService = memberValidationService;
        }

        /// <inheritdoc/>
        public async Task HandleAsync(AuthorizationHandlerContext context)
        {
            context = context ?? throw new ArgumentNullException(nameof(context));

            var oidClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

            var oidClaim = context.User.Claims.FirstOrDefault(p => oidClaimType == p.Type);

            foreach (var requirement in context.Requirements)
            {
                if (requirement is MustBeTeamMemberUserPolicyRequirement)
                {
                    if (context.Resource is AuthorizationFilterContext authorizationFilterContext)
                    {
                        // Wrap the request stream so that we can rewind it back to the start for regular request processing.
                        authorizationFilterContext.HttpContext.Request.EnableBuffering();

                        if (!string.IsNullOrEmpty(authorizationFilterContext.HttpContext.Request.QueryString.Value))
                        {
                            var requestQuery = authorizationFilterContext.HttpContext.Request.Query;
                            string groupId = requestQuery.Where(queryData => queryData.Key == "groupId").Select(queryData => queryData.Value.ToString()).FirstOrDefault();

                            // Check if current sign-in user is the part of team.
                            if (await this.ValidateUserIsPartOfTeamAsync(groupId, oidClaim.Value, authorizationFilterContext.HttpContext.Request.Headers["Authorization"].ToString()))
                            {
                                context.Succeed(requirement);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if current user is a member of a certain team.
        /// </summary>
        /// <param name="groupId">The group id of the team that the validator uses to check if the user is a member of the team. </param>
        /// <param name="userAadObjectId">The user's Azure Active Directory object id.</param>
        /// <param name="accessToken">User access token.</param>
        /// <returns>The flag indicates that the user is a part of certain team or not.</returns>
        private async Task<bool> ValidateUserIsPartOfTeamAsync(string groupId, string userAadObjectId, string accessToken)
        {
            // The key is generated by combining group id of team and user object id.
            bool isCacheEntryExists = this.memoryCache.TryGetValue(this.GetCacheKey(groupId, userAadObjectId), out bool isUserValidMember);
            if (!isCacheEntryExists)
            {
                // If cache duration is not specified then by default cache for 60 minutes.
                var cacheDurationInMinutes = TimeSpan.FromMinutes(this.botOptions.Value.CacheDurationInMinutes);
                cacheDurationInMinutes = cacheDurationInMinutes.Minutes <= 0 ? TimeSpan.FromMinutes(60) : cacheDurationInMinutes;

                isUserValidMember = await this.memberValidationService.ValidateMemberAsync(userAadObjectId, groupId, accessToken);
                this.memoryCache.Set(this.GetCacheKey(groupId, userAadObjectId), isUserValidMember, cacheDurationInMinutes);
            }

            return isUserValidMember;
        }

        /// <summary>
        /// // Generate key by combining groupId and user object id.
        /// </summary>
        /// <param name="groupId">The group id of team that the validator uses to check if the user is a member of the team. </param>
        /// <param name="userAadObjectId">The user's Azure Active Directory object id.</param>
        /// <returns>Generated key.</returns>
        private string GetCacheKey(string groupId, string userAadObjectId)
        {
            return CacheKeysConstants.TeamMember + groupId + userAadObjectId;
        }
    }
}