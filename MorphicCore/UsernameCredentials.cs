using System;
using System.Collections.Generic;
using System.Text;

namespace MorphicCore
{
    /// <summary>
    /// Username/password based credentials
    /// </summary>
    public class UsernameCredentials : ICredentials
    {
        public UsernameCredentials(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; set; }
        public string Password { get; set; }
    }
}
