using System;
using System.Collections.Generic;
using System.Text;

namespace MorphicCore
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
    }
}
