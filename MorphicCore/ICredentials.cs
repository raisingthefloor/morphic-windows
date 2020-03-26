using System;
using System.Collections.Generic;
using System.Text;

namespace MorphicCore
{
    public interface ICredentials
    {
    }

    public class UsernameCredentials: ICredentials
    {
        public UsernameCredentials(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class KeyCredentials: ICredentials
    {

        public KeyCredentials(string key)
        {
            Key = key;
        }

        public string Key { get; set; }
    }
}
