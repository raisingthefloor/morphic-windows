using System;
using System.Collections.Generic;
using System.Text;

namespace Morphic.Core
{
    /// <summary>
    /// A collection of settings that a morphic application manages and persists across app launches
    /// </summary>
    public interface IUserSettings
    {
        /// <summary>
        /// The logged in user id
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Get the Username for the given user id
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public string? GetUsernameForId(string userId);

        /// <summary>
        /// Save the given username for the given id
        /// </summary>
        /// <param name="username"></param>
        /// <param name="userId"></param>
        public void SetUsernameForId(string username, string userId);
    }
}
