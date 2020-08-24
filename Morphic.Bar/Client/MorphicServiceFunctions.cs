// MorphicServiceFunctions.cs: Functions for accessing the web app.
//
// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt

namespace Morphic.Bar.Client
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public static class MorphicServiceFunctions
    {
        public static async Task<string?> GetBar(this MorphicService morphicService)
        {
            morphicService.Logger.LogInformation("Loading the bar");

            try
            {
                // Get the user's communities
                UserCommunities communities = await morphicService.Get<UserCommunities>();

                // Get the bar for the first community
                if (communities.Communities.Count > 0)
                {
                    UserCommunity userCommunity = await morphicService.Get<UserCommunity>(new
                    {
                        communityId = communities.Communities.First().Id
                    });

                    return userCommunity.BarJson;
                }
            }
            catch (TaskCanceledException)
            {
                morphicService.Logger.LogInformation("Bar loading cancelled");
            }

            return null;
        }
    }
}
